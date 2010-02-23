using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace TextReader.ScrollingView {

class ScrollablePanel : Panel {
    private RowRenderer renderer;
    private ImageProvider imageProvider;
    private RowProvider rowProvider;

    private Brush backgroundBrush = new SolidBrush(Color.White);
    private Brush stripeBrush = new SolidBrush(Color.Gray);
    private int? viewStart;
    private bool mouseDown;
    private int downY;
    private int downOffs;
    private System.Windows.Forms.Timer timer;
    private LinkedList<MousePos> mousePos = new LinkedList<MousePos>();
    private long downTime;
    private Queue<Animation> animations = new Queue<Animation>();

    private const int TimerIntervalMS = 20;
    private const int SlowDownAcceleration = 1; // px / tick ^ 2
    private const int ElasticForce = 4; // bigger number = weaker force (must be > 1)
    private const int MaxMouseHistoryMS = 100;
    private const int FlickThreshold = 20; // px
    private const int ClippingRatio = 3; // offscreen bitmap height / view height
    private const int DragTreshold = 20; // px
    private const int DragTresholdMS = 500;

    public ScrollablePanel() {
        timer = new System.Windows.Forms.Timer();
        timer.Interval = TimerIntervalMS;
        timer.Tick += moveTimerTick;
        timer.Enabled = false;
        this.Enabled = false;
    }

    public RowProvider RowProvider {
        get {
            return rowProvider;
        }
        set {
            if (rowProvider != null) {
                rowProvider.OnContentChanged -= rowProviderContentChanged;
            }
            if (imageProvider != null) {
                imageProvider.Dispose();
            }
            rowProvider = value;
            rowProvider.OnContentChanged += rowProviderContentChanged;
            renderer = new RowRenderer(ClientRectangle.Width, ClientRectangle.Height * ClippingRatio, rowProvider);
            imageProvider = new ImageProvider(renderer, rowProvider);
            reinit();
            timer.Enabled = true;
            this.Enabled = true;
        }
    }
    public Row RowAt(ref int y) {
        if (imageProvider == null) {
            return null;
        }
        ImageResult res = imageProvider.Result;
        if (res.r == null) {
            return null;
        }
        int h = y + (int) viewStart + res.Offset;
        for (int i = 0; i < res.r.RowOffsets.Length; i++) {
            if (h >= res.r.RowOffsets[i] && h - res.r.RowOffsets[i] < res.r.Rows[i].Height) {
                h -= res.r.RowOffsets[i];
                y = h;
                return res.r.Rows[i];
            }
        }
        return null;
    }

    public Position Position {
        get {
            if (imageProvider == null) {
                return null;
            }
            return imageProvider.Position;
        }
        set {
            if (imageProvider == null) {
                return;
            }
            imageProvider.Position = value;
            if (viewStart != null) {
                imageProvider.Offset = -(int) viewStart;
            }
            imageProvider.EnqueueRedraw();
        }
    }

    public void Freeze() {
        timer.Enabled = false;
    }

    public void Unfreeze() {
        timer.Enabled = true;
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            if (timer != null) {
                timer.Dispose();
                timer = null;
            }
            if (imageProvider != null) {
                imageProvider.Dispose();
                imageProvider = null;
            }
            if (rowProvider != null) {
                rowProvider.OnContentChanged -= rowProviderContentChanged;
            }
        }
        base.Dispose(disposing);
    }

    private void reinit() {
        mousePos.Clear();
        viewStart = null;
        Invalidate();
        if (rowProvider == null) {
            return;
        }
        rowProvider.Width = ClientRectangle.Width;
        renderer.Width = ClientRectangle.Width;
        renderer.Height = ClientRectangle.Height * ClippingRatio;
    }

    private void moveTimerTick(object sender, EventArgs e) {
        if (!mouseDown && imageProvider != null) {
            if (animations.Count > 0) {
                if (animations.Peek().Step()) {
                    animations.Dequeue();
                    imageProvider.EnqueueRedraw();
                }
                Invalidate();
            }
            if (imageProvider.Ready) {
                ImageResult result = imageProvider.Result;
                imageProvider.ResetReady();
                if (viewStart == null) {
                    viewStart = Math.Max((result.r.Image.Height - this.ClientRectangle.Height) / 2, 0);
                    imageProvider.Offset = (int) -viewStart;
                }
                if (result.r.Offset < 0 && result.Offset < (int) -viewStart) {
                    animations.Clear();
                    animations.Enqueue(new ElasticAnimation((int) -viewStart, () => imageProvider.Offset, (x) => imageProvider.Offset = x));
                } else if (result.r.Offset > 0 && result.Offset > (int) viewStart) {
                    animations.Clear();
                    animations.Enqueue(new ElasticAnimation((int) viewStart, () => imageProvider.Offset, (x) => imageProvider.Offset = x));
                }
                Invalidate();
            }
        }
        while (mousePos.Count != 0 && Environment.TickCount - mousePos.First.Value.time > MaxMouseHistoryMS) {
            mousePos.RemoveFirst();
        }
    }

    protected override void OnPaint(PaintEventArgs e) {
        if (imageProvider != null) {
            ImageResult res = imageProvider.Result;
            if (viewStart != null && res.r != null) {
                int viewOffset = res.Offset + (int) viewStart;
                if (viewOffset < 0) {
                    e.Graphics.FillRectangle(backgroundBrush, 0, 0, this.ClientRectangle.Width, -viewOffset);
                    if (viewOffset < -ClientRectangle.Height) {
                        e.Graphics.FillRectangle(stripeBrush, 0, -viewOffset % ClientRectangle.Height, this.ClientRectangle.Width, 20);
                    }
                }
                Rectangle src = new Rectangle(0, viewOffset, this.ClientRectangle.Width, this.ClientRectangle.Height);
                e.Graphics.DrawImage(res.r.Image, 0, 0, src, GraphicsUnit.Pixel);
                if (res.r.Image.Height - viewOffset < this.ClientRectangle.Height) {
                    e.Graphics.FillRectangle(backgroundBrush, 0, res.r.Image.Height - viewOffset, this.ClientRectangle.Width, this.ClientRectangle.Height + viewOffset - res.r.Image.Height);
                    e.Graphics.FillRectangle(stripeBrush, 0, (res.r.Image.Height - viewOffset) % ClientRectangle.Height + ClientRectangle.Height, this.ClientRectangle.Width, 20);
                }
            }
        }
        base.OnPaint(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e) {
    }

    protected override void OnResize(EventArgs e) {
        base.OnResize(e);
        reinit();
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        if (e.Button == MouseButtons.Left) {
            mouseDown = true;
            downY = e.Y;
            downOffs = imageProvider.Offset;
            animations.Clear();
            mousePos.AddLast(new MousePos { time = Environment.TickCount, y = e.Y });
            downTime = Environment.TickCount;
        }
        base.OnMouseDown(e);
    }
    
    protected override void OnMouseMove(MouseEventArgs e) {
        if (mouseDown) {
            imageProvider.Offset = downOffs + downY - e.Y;
            mousePos.AddLast(new MousePos { time = Environment.TickCount, y = e.Y });
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e) {
        if (e.Button == MouseButtons.Left) {
            mouseDown = false;
            if (Math.Abs(e.Y - downY) < DragTreshold && Environment.TickCount - downTime < DragTresholdMS) { // is click
                int y = e.Y;
                Row row = RowAt(ref y);
                if (row != null) {
                    rowProvider.RowClicked(row, new MouseEventArgs(MouseButtons.Left, 0, e.X, y, 0));
                }
            } else {
                imageProvider.EnqueueRedraw();
                if (animations.Count == 0 && mousePos.Count != 0) {
                    int distance = mousePos.Last.Value.y - mousePos.First.Value.y;
                    int time = mousePos.Last.Value.time - mousePos.First.Value.time;
                    if (Math.Abs(distance) > FlickThreshold && time > 0) {
                        int speed = -distance * timer.Interval / time;
                        animations.Enqueue(new InertialAnimation(speed, () => imageProvider.Offset, (x) => imageProvider.Offset = x));
                    }
                }
                Invalidate();
            }
        }
        base.OnMouseUp(e);
    }

    private void rowProviderContentChanged(int direction) {
        mousePos.Clear();
        if (imageProvider != null) {
            ImageResult res = imageProvider.Result;
            if (res.r != null && direction != 0 && viewStart != null) {
                animations.Enqueue(new ChangeContentAnimation(res, Math.Sign(direction), this));
            }
        }
        viewStart = null;
        Invalidate();
    }

    private struct MousePos {
        public int time;
        public int y;
    }

    private interface Animation {
        bool Step();
    }

    private class ElasticAnimation : Animation {
        private int target;
        private Func<int> getter;
        private Action<int> setter;
        public ElasticAnimation(int target, Func<int> getter, Action<int> setter) {
            this.target = target;
            this.getter = getter;
            this.setter = setter;
        }
        public bool Step() {
            int current = getter();
            int distance = (target - current) / ElasticForce;
            if (distance == 0) {
                current += Math.Sign(target - current);
            } else {
                current += distance;
            }
            setter(current);
            if (current == target) {
                return true;
            }
            return false;
       }
    }
    
    private class InertialAnimation : Animation {
        private int speed;
        private bool neg;
        private Func<int> getter;
        private Action<int> setter;
        public InertialAnimation(int speed, Func<int> getter, Action<int> setter) {
            this.speed = Math.Abs(speed);
            this.neg = speed < 0;
            this.getter = getter;
            this.setter = setter;
        }
        public bool Step() {
            int current = getter();
            if (neg) {
                current -= speed;
            } else {
                current += speed;
            }
            speed -= SlowDownAcceleration;
            if (speed < 0) {
                speed = 0;
            }
            setter(current);
            return speed == 0;
        }
    }

    private class ChangeContentAnimation : Animation {
        private int direction;
        private Image current;
        private ScrollablePanel panel;
        public ChangeContentAnimation(ImageResult res, int direction, ScrollablePanel panel) {
            this.current = new Bitmap(panel.ClientRectangle.Width, panel.ClientRectangle.Height);
            Graphics g = Graphics.FromImage(this.current);
            int viewOffset = res.Offset + (int) panel.viewStart;
            Rectangle src = new Rectangle(0, viewOffset, panel.ClientRectangle.Width, panel.ClientRectangle.Height);
            g.FillRectangle(panel.backgroundBrush, panel.ClientRectangle);
            g.DrawImage(res.r.Image, 0, 0, src, GraphicsUnit.Pixel);
            g.Dispose();
            this.direction = direction;
            this.panel = panel;
        }
        public bool Step() {
            if (panel.imageProvider.Result.r == null) {
                return false;
            }
            panel.imageProvider.ForceRedraw();
            Image next = new Bitmap(panel.ClientRectangle.Width, panel.ClientRectangle.Height);
            Graphics g = Graphics.FromImage(next);
            g.FillRectangle(panel.backgroundBrush, panel.ClientRectangle);
            g.DrawImage(panel.imageProvider.Result.r.Image, 0, 0);
            g.Dispose();

            g = panel.CreateGraphics();
            int bound = panel.ClientRectangle.Width;
            while (bound > 0) {
                if (direction > 0) {
                    g.DrawImage(current, bound - panel.ClientRectangle.Width, 0);
                    g.DrawImage(next, bound, 0);
                } else {
                    g.DrawImage(current, panel.ClientRectangle.Width - bound, 0);
                    g.DrawImage(next, -bound, 0);
                }
                Thread.Sleep(20);
                bound -= 40;
            }
            g.Dispose();
            next.Dispose();
            current.Dispose();
            panel.Invalidate();
            return true;
        }
    }

    private class ImageResult {
        public RowRenderer.Result r;
        public int Offset;
    }
    private class ImageProvider : IDisposable {
        private AutoResetEvent task = new AutoResetEvent(false);
        private ManualResetEvent ready = new ManualResetEvent(false);
        private Mutex working = new Mutex();
        private Thread thread;
        private RowRenderer renderer;
        private RowProvider rowProvider;

        private int dirtyOffset;
        private RowRenderer.Result result;

        public ImageProvider(RowRenderer renderer, RowProvider rowProvider) {
            this.renderer = renderer;
            this.rowProvider = rowProvider;
            this.thread = new Thread(drawImage);
            this.thread.Priority = ThreadPriority.BelowNormal;
            this.thread.Start();
            EnqueueRedraw();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~ImageProvider() {
            Dispose(false);
        }
        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                if (thread != null) {
                    thread.Abort(); // Interrupt not supported on CF
                    thread = null;
                }
                if (task != null) {
                    task.Close();
                    task = null;
                }
                if (ready != null) {
                    ready.Close();
                    ready = null;
                }
                if (this.result != null) {
                    result.Dispose();
                    result = null;
                }
            }
        }
        private void drawImage() {
            while (true) {
                task.WaitOne();
                working.WaitOne();
                try {
                    int offset;
                    int startDirty;
                    lock (this) {
                        startDirty = dirtyOffset;
                        offset = this.Offset;
                    }
                    RowRenderer.Result rendererResult;
                    lock (rowProvider) {
                        rendererResult = renderer.DrawImage(offset);
                    }
                    lock (this) {
                        if (this.result != null) {
                            this.result.Image.Dispose();
                        }
                        this.result = rendererResult;
                        dirtyOffset = dirtyOffset - startDirty;
                    }
                    ready.Set();
                } finally {
                    working.ReleaseMutex();
                }
            }
        }
        public int Offset {
            get {
                lock (this) {
                    return (this.result != null ? this.result.Offset : 0) + dirtyOffset;
                }
            }
            set {
                lock (this) {
                    if (this.result != null) {
                        dirtyOffset = value - this.result.Offset;
                        if (Math.Abs(dirtyOffset) > this.result.Image.Height / 2) {
                            EnqueueRedraw();
                        }
                    } else {
                        dirtyOffset = value;
                    }
                }
            }
        }
        public void EnqueueRedraw() {
            ready.Reset();
            task.Set();
        }
        public void ResetReady() {
            ready.Reset();
        }
        public ImageResult Result { 
            get { 
                lock (this) {
                    return new ImageResult() { r = this.result, Offset = this.Offset }; 
                }
            } 
        }
        public void ForceRedraw() {
            ready.Reset();
            task.Set();
            ready.WaitOne();
        }
        public bool Dirty { get { lock (this) { return dirtyOffset != 0; } } }
        public bool Ready { get { return ready.WaitOne(0, false); } }
        public Position Position {
            get {
                lock (this) {
                    if (result != null) {
                        return new Pos() { dirtyOffset = dirtyOffset, pos = result.position };
                    } else {
                        return null;
                    }
                }
            }
            set {
                working.WaitOne();
                try {
                    ready.Reset();
                    result = null;
                    if (value is Pos) {
                        Pos p = (Pos) value;
                        renderer.Position = p.pos;
                        dirtyOffset = p.dirtyOffset;
                    } else {
                        renderer.Position = value;
                        dirtyOffset = 0;
                    }
                } finally {
                    working.ReleaseMutex();
                }
            }
        }
        private class Pos : Position {
            public int dirtyOffset;
            public Position pos;
        }
    }
}

}