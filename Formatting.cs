using System.Drawing;

namespace TextReader.Formatting {

public enum StyleName { 
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

public class Style {
    public StyleName Name { get; set; }
    public Font Font { get; set; }
    public ParagraphAlign Align { get; set; }
    public int Indent { get; set; }
    public int FirstLineIndent { get; set; }
    internal Style() { }
}

public class StyleFactory {
    public readonly Style Normal;
    public StyleFactory(Font baseFont) {
        Normal = new Style() { Font = baseFont, Align = ParagraphAlign.Left, Indent = 0, FirstLineIndent = 1 };
    }
    public Style createStyle(StyleName style, Style baseStyle) {
        return new Style() { Name = style, Font = new Font(baseStyle.Font, baseStyle.Font.Style), Align = ParagraphAlign.Left, Indent = 0, FirstLineIndent = 1 };
    }
}

}