using System.Drawing;

namespace TextReader.Formatting {

public enum StyleName { 
    Normal,
    Annotation, 
    Citation, 
    Dedication,
    Epigraph,
    Preformatted,
    Information,
    Letter,
    Poem,
    Sign,
    Table,
    Title,
    Heading1,
    Heading2,
    Heading3,
    Heading4,
    Heading5,
    SubTitle,
    Author,
    TableHeading,
    Emphasis,
    Strong
}

public enum ParagraphAlign {
    Left, 
    Right, 
    Center, 
    Justified
}

public interface Style {
    StyleName Name { get; }
    Font Font { get; }
}

public class ParagraphStyle : Style {
    public StyleName Name { get; internal set; }
    public Font Font { get; internal set; }
    public ParagraphAlign Align { get; internal set; }
    public int Indent { get; internal set; }
    public int FirstLineIndent { get; internal set; }
    internal ParagraphStyle(StyleName name) { 
        this.Name = name;
    }
}

public class CharacterStyle : Style {
    public StyleName Name { get; internal set; }
    public Font Font { get; internal set; }
    public CharacterStyle(StyleName name) {
        this.Name = name;
    }
}

public class StyleFactory {
    public readonly Style Normal;
    public StyleFactory(Font baseFont) {
        Normal = new ParagraphStyle(StyleName.Normal) { Font = baseFont, Align = ParagraphAlign.Left, Indent = 0, FirstLineIndent = 1 };
    }
    public Style createStyle(StyleName style, Style baseStyle) {
        switch (style) {
            case StyleName.Normal: return Normal;
            case StyleName.Annotation: return new ParagraphStyle(style) {
                Font = toggle(baseStyle.Font, FontStyle.Italic),
                Align = ParagraphAlign.Left,
                FirstLineIndent = 2,
                Indent = 1
            };
            case StyleName.Citation: return new ParagraphStyle(style) {
                Font = baseStyle.Font,
                Align = ParagraphAlign.Left,
                FirstLineIndent = 2,
                Indent = 1,
            };
            case StyleName.Dedication: return new ParagraphStyle(style) {
                Font = toggle(baseStyle.Font, FontStyle.Italic),
                Align = ParagraphAlign.Left,
                FirstLineIndent = 2,
                Indent = 2
            };
            case StyleName.Epigraph: return new ParagraphStyle(style) {
                Font = toggle(baseStyle.Font, FontStyle.Italic),
                Align = ParagraphAlign.Left,
                FirstLineIndent = 1,
                Indent = 1
            };
            case StyleName.Preformatted: return new ParagraphStyle(style) {
                Font = new Font("Courier New", baseStyle.Font.Size, FontStyle.Regular),
                Align = ParagraphAlign.Left,
            };
            case StyleName.Information: return new ParagraphStyle(style) {
                Font = baseStyle.Font,
                Align = ParagraphAlign.Center
            };
            case StyleName.Letter: return new ParagraphStyle(style) {
                Font = toggle(baseStyle.Font, FontStyle.Italic),
                Align = ParagraphAlign.Left,
                FirstLineIndent = 2,
                Indent = 1
            };
            case StyleName.Poem: return new ParagraphStyle(style) {
                Font = toggle(baseStyle.Font, FontStyle.Italic),
                Align = ParagraphAlign.Left,
                FirstLineIndent = 1,
                Indent = 1
            };
            case StyleName.Sign: return new ParagraphStyle(style) {
                Font = toggle(baseStyle.Font, FontStyle.Bold),
                Align = ParagraphAlign.Center,
            };
            case StyleName.Table: return new ParagraphStyle(style) {
                Font = new Font("Courier New", baseStyle.Font.Size, FontStyle.Regular),
                Align = ParagraphAlign.Left,
            };
            case StyleName.Title: return new ParagraphStyle(style) {
                Font = new Font(baseStyle.Font.Name, baseStyle.Font.Size * 2, FontStyle.Bold),
                Align = ParagraphAlign.Center
            };
            case StyleName.Heading1: return new ParagraphStyle(style) {
                Font = new Font(baseStyle.Font.Name, baseStyle.Font.Size * 3 / 2, FontStyle.Bold),
                Align = ParagraphAlign.Center
            };
            case StyleName.Heading2: return new ParagraphStyle(style) {
                Font = new Font(baseStyle.Font.Name, baseStyle.Font.Size + 2, FontStyle.Bold),
                Align = ParagraphAlign.Left,
                FirstLineIndent = 1,
                Indent = 1
            };
            case StyleName.Heading3: return new ParagraphStyle(style) {
                Font = new Font(baseStyle.Font.Name, baseStyle.Font.Size, FontStyle.Bold),
                Align = ParagraphAlign.Left,
                FirstLineIndent = 1,
                Indent = 1
            };
            case StyleName.Heading4: return new ParagraphStyle(style) {
                Font = new Font(baseStyle.Font.Name, baseStyle.Font.Size, FontStyle.Italic),
                Align = ParagraphAlign.Left,
                FirstLineIndent = 1,
                Indent = 1
            };
            case StyleName.Heading5: return new ParagraphStyle(style) {
                Font = new Font(baseStyle.Font.Name, baseStyle.Font.Size, FontStyle.Underline),
                Align = ParagraphAlign.Left,
                FirstLineIndent = 1,
                Indent = 1
            };
            case StyleName.SubTitle: return new ParagraphStyle(style) {
                Font = new Font(baseStyle.Font.Name, baseStyle.Font.Size, FontStyle.Bold),
                Align = ParagraphAlign.Center
            };
            case StyleName.Author: return new ParagraphStyle(style) {
                Font = baseStyle.Font,
                Align = ParagraphAlign.Right
            };
            case StyleName.TableHeading: return new ParagraphStyle(style) {
                Font = new Font(baseStyle.Font.Name, baseStyle.Font.Size, FontStyle.Bold),
                Align = ParagraphAlign.Left,
            };
            case StyleName.Emphasis: return new CharacterStyle(style) {
                Font = toggle(baseStyle.Font, FontStyle.Italic),
            };
            case StyleName.Strong: return new CharacterStyle(style) {
                Font = toggle(baseStyle.Font, FontStyle.Bold),
            };
        }
        return Normal;
    }
    private Font toggle(Font font, FontStyle feature) {
        if ((font.Style & feature) == feature) {
            return new Font(font.Name, font.Size, font.Style & feature);
        } else {
            return new Font(font.Name, font.Size, font.Style | feature);
        }
    }
}

}