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

    private double autoscrollSpeed = 0.03;
    private bool autoscroll;
    private Brush backgroundBrush = new SolidBrush(Color.White);
    private Brush stripeBrush = new SolidBrush(Color.Gray);
    private int? viewStart;
    private bool mouseDown;
    private int downY;
    private int lastY;
    private System.Windows.Forms.Timer timer;
    private LinkedList<MousePos> mousePos = new LinkedList<MousePos>();
    private long downTime;
    private Animator animator = new Animator();

    private const int TimerIntervalMS = 20;
    private const int SlowDownAcceleration = 1; // px / tick ^ 2
    private const int ElasticForce = 4; // bigger number = weaker force (must be > 1)
    private const int MaxMouseHistoryMS = 100;
    private const int FlickThreshold = 100; // px
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
        using (var res = imageProvider.Result) {
            return rowAt(ref y, res);
        }
    }
    private Row rowAt(ref int y, ImageProvider.ImageResult res) {
        if (res.r == null || viewStart == null) {
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
            if (viewStart != null) {
                return new Pos() { pos = imageProvider.Position, viewStart = (int) viewStart };
            } else {
                return imageProvider.Position;
            }
        }
        set {
            if (imageProvider == null) {
                return;
            }
            if (value is Pos) {
                Pos p = (Pos) value;
                imageProvider.Position = p.pos;
                viewStart = p.viewStart;
            } else {
                imageProvider.Position = value;
                viewStart = null;
            }
            imageProvider.EnqueueRedraw();
        }
    }
    private class Pos : Position {
        public int viewStart;
        public Position pos;
    }

    public bool Autoscroll {
        get { return autoscroll; }
        set {
            autoscroll = value;
            if (autoscroll) {
                animator.ContinuousAnimation = new ConstantSpeedAnimation(autoscrollSpeed * TimerIntervalMS, ClientRectangle.Height, imageProvider);
            } else {
                animator.ContinuousAnimation = null;
            }
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
        lock (rowProvider) {
            rowProvider.Width = ClientRectangle.Width;
        }
        renderer.Width = ClientRectangle.Width;
        renderer.Height = ClientRectangle.Height * ClippingRatio;
    }

    private void moveTimerTick(object sender, EventArgs e) {
        if (!mouseDown && imageProvider != null) {
            if (animator.Step(imageProvider)) {
                Invalidate();
            }
            if (imageProvider.Ready) {
                using (var result = imageProvider.Result) {
                    imageProvider.ResetReady();
                    if (viewStart == null) {
                        viewStart = Math.Max((result.r.Image.Height - this.ClientRectangle.Height) / 2, 0);
                        imageProvider.Offset = (int) -viewStart;
                    }
                    if (result.r.Offset < 0 && result.Offset < (int) -viewStart) {
                        animator.Reset();
                        animator.Add(new ElasticAnimation((int) -viewStart, imageProvider));
                        animator.ContinuousAnimation = null;
                    } else if (result.r.Offset > 0 && result.Offset > (int) viewStart) {
                        animator.Reset();
                        animator.Add(new ElasticAnimation((int) viewStart, imageProvider));
                        animator.ContinuousAnimation = null;
                    }
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
            using (var res = imageProvider.Result) {
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
            lastY = e.Y;
            animator.Reset();
            animator.Pause();
            mousePos.AddLast(new MousePos { time = Environment.TickCount, y = e.Y });
            downTime = Environment.TickCount;
        }
        base.OnMouseDown(e);
    }
    
    protected override void OnMouseMove(MouseEventArgs e) {
        if (mouseDown) {
            lock (imageProvider) {
                imageProvider.Offset += lastY - e.Y;
            }
            lastY = e.Y;
            mousePos.AddLast(new MousePos { time = Environment.TickCount, y = e.Y });
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e) {
        if (e.Button == MouseButtons.Left) {
            mouseDown = false;
            animator.Resume();
            if (Math.Abs(e.Y - downY) < DragTreshold && Environment.TickCount - downTime < DragTresholdMS) { // is click
                int y = e.Y;
                Row row = highlightRow(ref y);
                if (row != null) {
                    rowProvider.RowClicked(row, new MouseEventArgs(MouseButtons.Left, 0, e.X, y, 0));
                }
            } else {
                bool flick = false;
                if (mousePos.Count != 0) {
                    int distance = mousePos.Last.Value.y - mousePos.First.Value.y;
                    int time = mousePos.Last.Value.time - mousePos.First.Value.time;
                    if (Math.Abs(distance) > FlickThreshold && time > 0) {
                        int speed = -distance * timer.Interval / time;
                        if (Math.Abs(speed) > 10) {
                            double add = (Math.Abs(speed) - 10) * 0.1;
                            add *= add;
                            if (speed > 0) {
                                speed += (int) add;
                            } else {
                                speed -= (int) add;
                            }
                        }
                        animator.Add(new InertialAnimation(speed, imageProvider));
                        flick = true;
                    }
                }
                if (!flick && autoscroll && Environment.TickCount - downTime > 0) {
                    autoscrollSpeed = (5000 * autoscrollSpeed + downY - e.Y) / (double) (5000 + Environment.TickCount - downTime);
                    animator.ContinuousAnimation = new ConstantSpeedAnimation(autoscrollSpeed * TimerIntervalMS, ClientRectangle.Height, imageProvider);
                }
                if (!flick && !autoscroll) { // just move
                    imageProvider.EnqueueRedraw();
                }
            } 
                
            Invalidate();
        }
        base.OnMouseUp(e);
    }

    private Row highlightRow(ref int y) {
        Row row = null;
        using (var res = imageProvider.Result) {
            row = rowAt(ref y, res);
            if (row != null) {
                int index = Array.IndexOf(res.r.Rows, row);
                int rowTop = res.r.RowOffsets[index];
                using (Graphics g = Graphics.FromImage(res.r.Image)) {
                    row.Draw(g, rowTop, true);
                }
            }
        }
        if (row != null) {
            Refresh();
        }
        return row;
    }

    private void rowProviderContentChanged(int direction) {
        mousePos.Clear();
        if (imageProvider != null) {
            using (var res = imageProvider.Result) {
                if (res.r != null && direction != 0 && viewStart != null) {
                    animator.Add(new ChangeContentAnimation(res, Math.Sign(direction), this));
                }
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

    private class Animator {
        private Queue<Animation> animations = new Queue<Animation>();
        private Animation continuousAnimation;
        private bool paused;

        public Animation ContinuousAnimation {
            get { return continuousAnimation; }
            set { continuousAnimation = value; }
        }

        public void Add(Animation animation) {
            animations.Enqueue(animation);
        }

        public bool Step(ImageProvider imageProvider) {
            if (paused) {
                return false;
            }
            if (animations.Count > 0) {
                if (animations.Peek().Step()) {
                    animations.Dequeue();
                    imageProvider.EnqueueRedraw();
                }
                return true;
            } else if (continuousAnimation != null) {
                if (continuousAnimation.Step()) {
                    animations.Dequeue();
                    imageProvider.EnqueueRedraw();
                }
                return true;
            } else {
                return false;
            }
        }

        public void Reset() {
            animations.Clear();
        }

        public void Pause() {
            paused = true;
        }

        public void Resume() {
            paused = false;
        }
    }

    private abstract class OffsetAnimation : Animation {
        protected ImageProvider imageProvider;
        protected OffsetAnimation(ImageProvider imageProvider) {
            this.imageProvider = imageProvider;
        }
        public bool Step() {
            lock (imageProvider) {
                int current = imageProvider.Offset;
                bool done = move(ref current);
                imageProvider.Offset = current;
                return done;
            }
        }
        protected abstract bool move(ref int current);
    }

    private class ElasticAnimation : OffsetAnimation {
        private int target;
        private bool drawNotified;
        public ElasticAnimation(int target, ImageProvider imageProvider) : base(imageProvider) {
            this.target = target;
        }
        protected override bool move(ref int current) {
            if (!drawNotified) {
                imageProvider.EnqueueRedraw(target);
                drawNotified = true;
            }
            int distance = (target - current) / ElasticForce;
            if (distance == 0) {
                current += Math.Sign(target - current);
            } else {
                current += distance;
            }
            if (current == target) {
                return true;
            }
            return false;
       }
    }
    
    private class InertialAnimation : OffsetAnimation {
        private int speed;
        private bool neg;
        public InertialAnimation(int speed, ImageProvider imageProvider) : base(imageProvider) {
            this.speed = Math.Abs(speed);
            this.neg = speed < 0;
        }
        protected bool next(ref int x, ref int speed) {
            if (neg) {
                x -= speed;
            } else {
                x += speed;
            }
            speed -= SlowDownAcceleration;
            if (speed < 0) {
                speed = 0;
            }
            return speed == 0;
        }
        protected override bool move(ref int current) {
            int timeToDrawInTicks = imageProvider.TimeToDraw / TimerIntervalMS;
            int s = speed;
            int c = current;
            int t = 0;
            while (t < timeToDrawInTicks && !next(ref c, ref s)) {
                t++;
            }
            imageProvider.EnqueueRedraw(c);
            return next(ref current, ref speed);
        }
    }

    private class ConstantSpeedAnimation : OffsetAnimation {
        private double speed;
        private double deficit;
        private int visibleHeight;
        private bool dirty;
        public ConstantSpeedAnimation(double speed, int visibleHeight, ImageProvider imageProvider) : base(imageProvider) {
            this.speed = speed;
            this.visibleHeight = visibleHeight;
        }
        protected override bool move(ref int current) {
            double newValue = current + speed + deficit;
            int n = (int) newValue;
            deficit = newValue - n;
            current = n;
            int futureDiff = (int) (speed * imageProvider.TimeToDraw / TimerIntervalMS);
            if (Math.Abs(imageProvider.DirtyOffset + futureDiff) >= visibleHeight) {
                if (!dirty) {
                    dirty = true;
                    imageProvider.EnqueueRedraw(imageProvider.Offset + futureDiff);
                }
            } else {
                dirty = false;
            }
            return speed == 0;
        }
    }

    private class ChangeContentAnimation : Animation {
        private int direction;
        private Image current;
        private ScrollablePanel panel;
        public ChangeContentAnimation(ImageProvider.ImageResult res, int direction, ScrollablePanel panel) {
            this.current = new Bitmap(panel.ClientRectangle.Width, panel.ClientRectangle.Height);
            using (var g = Graphics.FromImage(this.current)) {
                int viewOffset = res.Offset + (int) panel.viewStart;
                Rectangle src = new Rectangle(0, viewOffset, panel.ClientRectangle.Width, panel.ClientRectangle.Height);
                g.FillRectangle(panel.backgroundBrush, panel.ClientRectangle);
                g.DrawImage(res.r.Image, 0, 0, src, GraphicsUnit.Pixel);
                g.Dispose();
            }
            this.direction = direction;
            this.panel = panel;
        }
        public bool Step() {
            using (var res = panel.imageProvider.Result) {
                if (res.r == null) {
                    return false;
                }
            }
            panel.imageProvider.ForceRedraw();
            using (Image next = new Bitmap(panel.ClientRectangle.Width, panel.ClientRectangle.Height)) {
                using (var g = Graphics.FromImage(next)) {
                    g.FillRectangle(panel.backgroundBrush, panel.ClientRectangle);
                    using (var res = panel.imageProvider.Result) {
                        g.DrawImage(res.r.Image, 0, 0);
                    }
                }

                using (var g = panel.CreateGraphics()) {
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
                }
            }
            current.Dispose();
            panel.Invalidate();
            return true;
        }
    }

    private class ImageProvider : IDisposable {
        private AutoResetEvent task = new AutoResetEvent(false);
        private ManualResetEvent ready = new ManualResetEvent(false);
        private Mutex working = new Mutex();
        private Thread thread;
        private RowRenderer renderer;
        private RowProvider rowProvider;

        private int dirtyOffset;
        private RowRenderer.Result rendererResult;
        private ImageResult result;
        private int? futureDirty;
        private int timeToDraw;

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
                if (rendererResult != null) {
                    rendererResult.Dispose();
                    rendererResult = null;
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
                        int resultOffset = this.rendererResult != null ? this.rendererResult.Offset : 0;
                        if (futureDirty != null) {
                            offset = resultOffset + (int) futureDirty;
                            futureDirty = null;
                        } else {
                            offset = resultOffset + dirtyOffset;
                        }
                        startDirty = offset - resultOffset;
                    }
                    long time = Environment.TickCount;
                    RowRenderer.Result rendererResult;
                    lock (rowProvider) {
                        rendererResult = renderer.DrawImage(offset);
                        Thread.Sleep(1000);
                    }
                    lock (this) {
                        this.timeToDraw = (int) (Environment.TickCount - time);
                        if (result == null) {
                            if (this.rendererResult != null) {
                                this.rendererResult.Dispose();
                            }
                        } // else disposing the result will dispose the rendererResult
                        this.rendererResult = rendererResult;
                        dirtyOffset = dirtyOffset - startDirty;
                        if (futureDirty != null) {
                            futureDirty = futureDirty - startDirty;
                        }
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
                    return (this.rendererResult != null ? this.rendererResult.Offset : 0) + dirtyOffset;
                }
            }
            set {
                lock (this) {
                    if (this.rendererResult != null) {
                        dirtyOffset = value - this.rendererResult.Offset;
                        if (Math.Abs(dirtyOffset) > this.rendererResult.Image.Height / ClippingRatio) {
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
        public void EnqueueRedraw(int futureOffset) {
            lock (this) {
                int resultOffset = this.rendererResult != null ? this.rendererResult.Offset : 0;
                this.futureDirty = futureOffset - resultOffset;
                task.Set();
            }
        }
        public void ResetReady() {
            ready.Reset();
        }
        public ImageResult Result { 
            get { 
                lock (this) {
                    if (result != null) {
                        throw new ArgumentException("Dispose the previous Result first");
                    }
                    this.result = new ImageResult() { r = this.rendererResult, Offset = this.Offset, parent = this };
                    return this.result;
                }
            } 
        }
        public void ForceRedraw() {
            ready.Reset();
            task.Set();
            ready.WaitOne();
        }
        public int DirtyOffset { get { lock (this) { return dirtyOffset; } } }
        public bool Ready { get { return ready.WaitOne(0, false); } }
        public int TimeToDraw { get { return timeToDraw; } }
        public Position Position {
            get {
                lock (this) {
                    if (rendererResult != null) {
                        return new Pos() { dirtyOffset = Offset, pos = rendererResult.position };
                    } else {
                        return null;
                    }
                }
            }
            set {
                working.WaitOne();
                try {
                    ready.Reset();
                    rendererResult = null;
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
        public class ImageResult : IDisposable {
            public RowRenderer.Result r;
            public int Offset;
            public ImageProvider parent;
            public void Dispose() {
                lock (parent) {
                    if (parent.result != this) {
                        throw new ArgumentException("Trying to dispose inactive Result");
                    }
                    if (this.r != null && this.r != parent.rendererResult) {
                        r.Dispose();
                    }
                    parent.result = null;
                }
            }
        }
    }
}

}