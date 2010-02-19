using System;
using System.Collections.Generic;
using System.Drawing;

using TextReader.Formatting;

namespace TextReader.Parsing {

public class SFBParser : IScrollable<Token> {
    private static readonly IDictionary<string, string> blockMarkers = new Dictionary<string, string>() {
        { "A", StyleName.Annotation.ToString() }, 
        { "C", StyleName.Citation.ToString() },
        { "D", StyleName.Dedication.ToString() },
        { "E", StyleName.Epigraph.ToString() },
        { "F", StyleName.Preformatted.ToString() },
        { "I", StyleName.Information.ToString() },
        { "L", StyleName.Letter.ToString() },
        { "P", StyleName.Poem.ToString() },
        { "S", StyleName.Sign.ToString() },
        { "T", StyleName.Table.ToString() }
    };
    private static readonly IDictionary<string, string> rowMarkers = new Dictionary<string, string>() {
        { "|", StyleName.Title.ToString() },
        { ">", StyleName.Heading1.ToString() },
        { ">>", StyleName.Heading2.ToString() },
        { ">>>", StyleName.Heading3.ToString() },
        { ">>>>", StyleName.Heading4.ToString() },
        { ">>>>>", StyleName.Heading5.ToString() },
        { "#", StyleName.SubTitle.ToString() },
        { "@", StyleName.Author.ToString() },
        { "!", StyleName.TableHeading.ToString() },
        { "F", StyleName.Preformatted.ToString() },
        { "S", StyleName.Sign.ToString() }
    };

    private IScrollable<Token> tokenScroller;

    public SFBParser(IScrollable<string> lineSplitter) {
        CachedMappingScrollable<List<Token>, string> lineParser = new CachedMappingScrollable<List<Token>, string>(lineSplitter, parseLine);
        tokenScroller = new SplittingScrollable<Token, List<Token>>(lineParser);
    }

    private List<Token> parseLine(string line) {
        foreach (string blockMarker in blockMarkers.Keys) {
            if (line.Equals(blockMarker + ">")) {
                return new List<Token>() { new Token(blockMarkers[blockMarker], TokenType.Begin) };
            }
            if (line.Equals(blockMarker + "$")) {
                return new List<Token>() { new Token(blockMarkers[blockMarker], TokenType.End) };
            }
        }

        List<Token> result = new List<Token>();
        foreach (string rowMarker in rowMarkers.Keys) {
            if (line.StartsWith(rowMarker + "\t")) {
                result.Add(new Token(rowMarkers[rowMarker], TokenType.Begin));
                result.Add(new Token(Token.PARAGRAPH, TokenType.Begin));
                result.AddRange(tokenize(line.Substring(rowMarker.Length)));
                result.Add(new Token(Token.PARAGRAPH, TokenType.End));
                result.Add(new Token(rowMarkers[rowMarker], TokenType.End));
                return result;
            }
        }
        result.Add(new Token(Token.PARAGRAPH, TokenType.Begin));
        result.AddRange(tokenize(line));
        result.Add(new Token(Token.PARAGRAPH, TokenType.End));
        return result;
    }

    private IEnumerable<string> words(string line) {
        string lineText = line.Trim();
        int lineIndex = 0;
        while (lineIndex < lineText.Length) {
            int i = lineIndex;
            while (i < lineText.Length && !Char.IsWhiteSpace(lineText[i])) {
                i++;
            }
            string word = lineText.Substring(lineIndex, i - lineIndex);
            while (i < lineText.Length && Char.IsWhiteSpace(lineText[i])) {
                i++;
            }
            lineIndex = i;
            yield return word;
        }
    }

    private IEnumerable<Token> tokenize(string line) {
        var styles = new Stack<StyleName>();
        foreach (string word in words(line)) {
            string w = word;
            if (w.StartsWith("__")) {
                styles.Push(StyleName.Strong);
                w = w.Substring(2);
                yield return new Token(StyleName.Strong.ToString(), TokenType.Begin);
            } else if (w.StartsWith("_")) {
                styles.Push(StyleName.Emphasis);
                w = w.Substring(1);
                yield return new Token(StyleName.Emphasis.ToString(), TokenType.Begin);
            }
            StyleName? c = null;
            if (w.EndsWith("__")) {
                w = w.Substring(0, w.Length - 2);
                c = StyleName.Strong;
            } else if (w.EndsWith("_")) {
                w = w.Substring(0, w.Length - 1);
                c = StyleName.Emphasis;
            }
            yield return new Token(w, TokenType.Word);
            if (c != null) {
                while (styles.Count > 0 && styles.Peek() != StyleName.Strong) {
                    yield return new Token(styles.Pop().ToString(), TokenType.End);
                }
                if (styles.Count > 0) {
                    yield return new Token(styles.Pop().ToString(), TokenType.End);
                }
            }
        }
        while (styles.Count > 0) {
            yield return new Token(styles.Pop().ToString(), TokenType.End);
        }
    }
    public Token Current { get { return tokenScroller.Current; } }
    public bool IsFirst { get { return tokenScroller.IsFirst; } }
    public bool IsLast { get { return tokenScroller.IsLast; } }
    public void ToNext() {
        tokenScroller.ToNext();
    }
    public void ToPrev() {
        tokenScroller.ToPrev();
    }
    public Position Position { 
        get { return tokenScroller.Position; }
        set { tokenScroller.Position = value; }
    }
}

}