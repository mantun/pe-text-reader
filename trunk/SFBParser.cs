using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;

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
    private static readonly Regex strongStart = new Regex(@"^([^\w\d]*)__(.+)$", RegexOptions.Compiled);
    private static readonly Regex emphasisStart = new Regex(@"^([^\w\d]*)_(.+)$", RegexOptions.Compiled);
    private static readonly Regex strikeStart = new Regex(@"^([^\w\d]*)-(.+)$", RegexOptions.Compiled);
    private static readonly Regex strongEnd = new Regex(@"^(.+)__([^\w\d]*)$", RegexOptions.Compiled);
    private static readonly Regex emphasisEnd = new Regex(@"^(.+)_([^\w\d]*)$", RegexOptions.Compiled);
    private static readonly Regex strikeEnd = new Regex(@"^(.+)-([^\w\d]*)$", RegexOptions.Compiled);

    private static readonly IDictionary<Regex, StyleName> charStyleStart = new Dictionary<Regex, StyleName>() {
        { strongStart, StyleName.Strong },
        { emphasisStart, StyleName.Emphasis },
        { strikeStart, StyleName.Strike },
    };
    private static readonly IDictionary<Regex, StyleName> charStyleEnd = new Dictionary<Regex, StyleName>() {
        { strongEnd, StyleName.Strong },
        { emphasisEnd, StyleName.Emphasis },
        { strikeEnd, StyleName.Strike },
    };

    private IScrollable<Token> tokenScroller;

    public SFBParser(IScrollable<string> lineSplitter) {
        var lineParser = new CachedMappingScrollable<List<Token>, string>(new CachingScrollable<string>(lineSplitter, 100), parseLine);
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
            while (i < lineText.Length && !Char.IsWhiteSpace(lineText, i)) {
                i++;
            }
            string word = lineText.Substring(lineIndex, i - lineIndex);
            while (i < lineText.Length && Char.IsWhiteSpace(lineText, i)) {
                i++;
            }
            lineIndex = i;
            yield return word;
        }
    }

    private IEnumerable<Token> tokenize(string line) {
        var styles = new Stack<StyleName>();
        foreach (string word in words(line)) {
            if (!word.Contains("_") && !word.Contains("-")) {
                yield return new Token(word, TokenType.Word);
                continue;
            }

            string w = word;
            foreach (var p in charStyleStart) {
                if (checkRegex(p.Key, ref w)) {
                    styles.Push(p.Value);
                    yield return new Token(p.Value.ToString(), TokenType.Begin);
                }
            }
            LinkedList<StyleName> endStyles = new LinkedList<StyleName>();
            foreach (var p in charStyleEnd) {
                if (checkRegex(p.Key, ref w)) {
                    endStyles.AddFirst(p.Value);
                }
            }

            yield return new Token(w, TokenType.Word);

            foreach (var end in endStyles) {
                while (styles.Count > 0 && styles.Peek() != end) {
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

    private bool checkRegex(Regex r, ref string s) {
        var m = r.Match(s);
        if (m.Success) {
            s = m.Groups[1].Value + m.Groups[2].Value;
        }
        return m.Success;
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