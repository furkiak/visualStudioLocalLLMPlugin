using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LLLMPluginModern.Core.Text
{
    public enum MarkdownBlockKind
    {
        Text,
        Code
    }

    public sealed class MarkdownBlock
    {
        public MarkdownBlock(MarkdownBlockKind kind, string content, string language = "", bool isComplete = true)
        {
            Kind = kind;
            Content = content ?? "";
            Language = language ?? "";
            IsComplete = isComplete;
        }

        public MarkdownBlockKind Kind { get; }
        public string Content { get; }
        public string Language { get; }

        /// <summary>False for a code fence that has not been closed yet (still streaming).</summary>
        public bool IsComplete { get; }
    }

    public enum MarkdownLineKind
    {
        Paragraph,
        Heading,
        Bullet,
        Numbered,
        Quote,
        Rule,
        Blank
    }

    public enum MarkdownSpanKind
    {
        Text,
        Bold,
        Italic,
        Code
    }

    public struct MarkdownSpan
    {
        public MarkdownSpan(MarkdownSpanKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public MarkdownSpanKind Kind { get; }
        public string Text { get; }
    }

    /// <summary>A deliberately small Markdown reader: fenced code blocks plus the handful of
    /// line and inline constructs LLMs actually use. Fast enough to re-run on every streaming update.</summary>
    public static class Markdown
    {
        public static IReadOnlyList<MarkdownBlock> ParseBlocks(string markdown)
        {
            var blocks = new List<MarkdownBlock>();
            if (string.IsNullOrEmpty(markdown))
                return blocks;

            var lines = TextTools.SplitLines(markdown);
            var text = new StringBuilder();
            var code = new StringBuilder();
            string fence = null;
            string language = "";

            foreach (var line in lines)
            {
                if (fence == null)
                {
                    if (TryOpenFence(line, out fence, out language))
                    {
                        FlushText(blocks, text);
                        code.Clear();
                        continue;
                    }
                    if (text.Length > 0)
                        text.Append('\n');
                    text.Append(line);
                }
                else
                {
                    if (IsClosingFence(line, fence))
                    {
                        blocks.Add(new MarkdownBlock(MarkdownBlockKind.Code, code.ToString(), language));
                        fence = null;
                        continue;
                    }
                    if (code.Length > 0)
                        code.Append('\n');
                    code.Append(line);
                }
            }

            if (fence != null)
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Code, code.ToString(), language, isComplete: false));
            else
                FlushText(blocks, text);

            return blocks;
        }

        /// <summary>
        /// The text to insert into the editor for a model answer: the code blocks when there are any
        /// (joined by a blank line), otherwise the whole answer.
        /// </summary>
        public static string ExtractCode(string markdown, out bool containsCode)
        {
            var codeBlocks = ParseBlocks(markdown).Where(b => b.Kind == MarkdownBlockKind.Code && b.Content.Trim().Length > 0).ToList();
            containsCode = codeBlocks.Count > 0;
            if (!containsCode)
                return (markdown ?? "").Trim();
            return string.Join("\n\n", codeBlocks.Select(b => b.Content.TrimEnd()));
        }

        private static void FlushText(List<MarkdownBlock> blocks, StringBuilder text)
        {
            string value = text.ToString().Trim('\n', '\r');
            if (value.Trim().Length > 0)
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Text, value));
            text.Clear();
        }

        private static bool TryOpenFence(string line, out string fence, out string language)
        {
            fence = null;
            language = "";
            string trimmed = line.TrimStart();
            if (line.Length - trimmed.Length > 3)
                return false;

            char c = trimmed.Length > 0 ? trimmed[0] : '\0';
            if (c != '`' && c != '~')
                return false;

            int count = 0;
            while (count < trimmed.Length && trimmed[count] == c)
                count++;
            if (count < 3)
                return false;

            string info = trimmed.Substring(count).Trim();
            if (c == '`' && info.IndexOf('`') >= 0)
                return false;

            fence = new string(c, count);
            int space = info.IndexOfAny(new[] { ' ', '\t', '{' });
            language = (space >= 0 ? info.Substring(0, space) : info).Trim();
            return true;
        }

        private static bool IsClosingFence(string line, string fence)
        {
            string trimmed = line.Trim();
            if (trimmed.Length < fence.Length)
                return false;
            char c = fence[0];
            for (int i = 0; i < trimmed.Length; i++)
            {
                if (trimmed[i] != c)
                    return false;
            }
            return true;
        }

        public static MarkdownLineKind ClassifyLine(string line, out string content, out int level)
        {
            level = 0;
            content = line ?? "";
            string trimmed = content.Trim();
            if (trimmed.Length == 0)
                return MarkdownLineKind.Blank;

            if (trimmed.Length >= 3 && (trimmed.All(ch => ch == '-' || ch == ' ') || trimmed.All(ch => ch == '*' || ch == ' ') || trimmed.All(ch => ch == '_' || ch == ' '))
                && trimmed.Count(ch => ch != ' ') >= 3)
            {
                content = "";
                return MarkdownLineKind.Rule;
            }

            if (trimmed[0] == '#')
            {
                int hashes = 0;
                while (hashes < trimmed.Length && trimmed[hashes] == '#')
                    hashes++;
                if (hashes <= 6 && (hashes == trimmed.Length || trimmed[hashes] == ' '))
                {
                    level = hashes;
                    content = trimmed.Substring(hashes).Trim().TrimEnd('#').Trim();
                    return MarkdownLineKind.Heading;
                }
            }

            if (trimmed[0] == '>')
            {
                content = trimmed.Substring(1).TrimStart();
                return MarkdownLineKind.Quote;
            }

            string indent = TextTools.LeadingWhitespace(content);
            int depth = indent.Replace("\t", "    ").Length / 2;

            if ((trimmed[0] == '-' || trimmed[0] == '*' || trimmed[0] == '+') && trimmed.Length > 1 && trimmed[1] == ' ')
            {
                level = depth;
                content = trimmed.Substring(2).TrimStart();
                return MarkdownLineKind.Bullet;
            }

            int digits = 0;
            while (digits < trimmed.Length && char.IsDigit(trimmed[digits]))
                digits++;
            if (digits > 0 && digits < 4 && digits + 1 < trimmed.Length && (trimmed[digits] == '.' || trimmed[digits] == ')') && trimmed[digits + 1] == ' ')
            {
                level = depth;
                content = trimmed.Substring(0, digits + 1) + " " + trimmed.Substring(digits + 2).TrimStart();
                return MarkdownLineKind.Numbered;
            }

            content = trimmed;
            return MarkdownLineKind.Paragraph;
        }

        /// <summary>Splits a line into plain, **bold**, *italic* and `code` spans.</summary>
        public static List<MarkdownSpan> ParseInline(string text)
        {
            var spans = new List<MarkdownSpan>();
            if (string.IsNullOrEmpty(text))
                return spans;

            var plain = new StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];

                if (c == '\\' && i + 1 < text.Length && IsEscapable(text[i + 1]))
                {
                    plain.Append(text[i + 1]);
                    i += 2;
                    continue;
                }

                if (c == '`')
                {
                    int ticks = CountRun(text, i, '`');
                    string marker = new string('`', ticks);
                    int close = text.IndexOf(marker, i + ticks, StringComparison.Ordinal);
                    if (close > i)
                    {
                        Flush(spans, plain);
                        spans.Add(new MarkdownSpan(MarkdownSpanKind.Code, text.Substring(i + ticks, close - i - ticks).Trim()));
                        i = close + ticks;
                        continue;
                    }
                }

                if ((c == '*' || c == '_') && i + 1 < text.Length && text[i + 1] == c)
                {
                    string marker = new string(c, 2);
                    int close = text.IndexOf(marker, i + 2, StringComparison.Ordinal);
                    if (close > i + 2)
                    {
                        Flush(spans, plain);
                        spans.Add(new MarkdownSpan(MarkdownSpanKind.Bold, text.Substring(i + 2, close - i - 2)));
                        i = close + 2;
                        continue;
                    }
                }

                if ((c == '*' || c == '_') && i + 1 < text.Length && text[i + 1] != ' ' && (i == 0 || !char.IsLetterOrDigit(text[i - 1])))
                {
                    int close = text.IndexOf(c, i + 1);
                    if (close > i + 1 && text[close - 1] != ' ' && (close + 1 >= text.Length || !char.IsLetterOrDigit(text[close + 1])))
                    {
                        Flush(spans, plain);
                        spans.Add(new MarkdownSpan(MarkdownSpanKind.Italic, text.Substring(i + 1, close - i - 1)));
                        i = close + 1;
                        continue;
                    }
                }

                if (c == '[')
                {
                    int closeBracket = text.IndexOf("](", i + 1, StringComparison.Ordinal);
                    int closeParen = closeBracket > 0 ? text.IndexOf(')', closeBracket + 2) : -1;
                    if (closeBracket > i && closeParen > closeBracket)
                    {
                        plain.Append(text, i + 1, closeBracket - i - 1);
                        i = closeParen + 1;
                        continue;
                    }
                }

                plain.Append(c);
                i++;
            }

            Flush(spans, plain);
            return spans;
        }

        private static bool IsEscapable(char c) => "\\`*_{}[]()#+-.!>".IndexOf(c) >= 0;

        private static int CountRun(string text, int start, char c)
        {
            int n = 0;
            while (start + n < text.Length && text[start + n] == c)
                n++;
            return n;
        }

        private static void Flush(List<MarkdownSpan> spans, StringBuilder plain)
        {
            if (plain.Length == 0)
                return;
            spans.Add(new MarkdownSpan(MarkdownSpanKind.Text, plain.ToString()));
            plain.Clear();
        }
    }
}
