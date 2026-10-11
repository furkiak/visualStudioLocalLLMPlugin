using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LLLMPluginModern.Core.Text
{
    /// <summary>
    /// Line based text transformations. Every tool keeps the original line break style and
    /// preserves a trailing line break, so applying the result never merges the line that
    /// follows the selection.
    /// </summary>
    public static class TextTools
    {
        public static string DetectNewLine(string text, string fallback = null)
        {
            if (!string.IsNullOrEmpty(text))
            {
                int lf = text.IndexOf('\n');
                if (lf > 0 && text[lf - 1] == '\r')
                    return "\r\n";
                if (lf >= 0)
                    return "\n";
                if (text.IndexOf('\r') >= 0)
                    return "\r";
            }
            return fallback ?? Environment.NewLine;
        }

        /// <summary>Splits on CRLF, LF or CR. "a\nb\n" yields ["a", "b", ""].</summary>
        public static List<string> SplitLines(string text)
        {
            var lines = new List<string>();
            if (text == null)
                return lines;

            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r' || c == '\n')
                {
                    lines.Add(text.Substring(start, i - start));
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    start = i + 1;
                }
            }
            lines.Add(text.Substring(start));
            return lines;
        }

        public static string NormalizeNewLines(string text, string newLine)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? "";
            return string.Join(newLine, SplitLines(text));
        }

        /// <summary>Runs <paramref name="transform"/> over the content lines, keeping the trailing line break.</summary>
        private static string TransformLines(string text, Func<List<string>, IEnumerable<string>> transform)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? "";

            string newLine = DetectNewLine(text);
            var lines = SplitLines(text);
            bool trailingBreak = lines.Count > 1 && lines[lines.Count - 1].Length == 0;
            if (trailingBreak)
                lines.RemoveAt(lines.Count - 1);

            string result = string.Join(newLine, transform(lines));
            return trailingBreak ? result + newLine : result;
        }

        /// <summary>Removes repeated lines, keeping the first occurrence. Blank lines are left untouched.</summary>
        public static string RemoveDuplicateLines(string text)
        {
            return TransformLines(text, lines =>
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                return lines.Where(line => line.Trim().Length == 0 || seen.Add(line)).ToList();
            });
        }

        /// <summary>For every line containing <paramref name="target"/>, emits one copy per replacement.</summary>
        public static string ReplicateLines(string text, string target, IList<string> replacements)
        {
            if (string.IsNullOrEmpty(target) || replacements == null || replacements.Count == 0)
                return text ?? "";

            return TransformLines(text, lines =>
            {
                var result = new List<string>(lines.Count * replacements.Count);
                foreach (var line in lines)
                {
                    if (line.Contains(target))
                        result.AddRange(replacements.Select(r => line.Replace(target, r)));
                    else
                        result.Add(line);
                }
                return result;
            });
        }

        public static string EraseText(string text, string word)
        {
            if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(text))
                return text ?? "";
            return TransformLines(text, lines => lines.Select(line => line.Replace(word, string.Empty)).ToList());
        }

        public static string AddToLines(string text, string addition, bool atLineStart)
        {
            if (string.IsNullOrEmpty(addition) || string.IsNullOrEmpty(text))
                return text ?? "";
            return TransformLines(text, lines => lines.Select(line => atLineStart ? addition + line : line + addition).ToList());
        }

        public static string SortLines(string text, bool descending)
        {
            return TransformLines(text, lines =>
            {
                var comparer = Comparer<string>.Create((a, b) =>
                {
                    int c = string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase);
                    return c != 0 ? c : string.CompareOrdinal(a, b);
                });
                return descending ? lines.OrderByDescending(l => l, comparer).ToList() : lines.OrderBy(l => l, comparer).ToList();
            });
        }

        public static string ReverseLines(string text)
        {
            return TransformLines(text, lines => Enumerable.Reverse(lines).ToList());
        }

        public static string RemoveEmptyLines(string text)
        {
            return TransformLines(text, lines => lines.Where(l => l.Trim().Length > 0).ToList());
        }

        public static string TrimTrailingWhitespace(string text)
        {
            return TransformLines(text, lines => lines.Select(l => l.TrimEnd()).ToList());
        }

        public static string ToUpper(string text) => text?.ToUpperInvariant() ?? "";

        public static string ToLower(string text) => text?.ToLowerInvariant() ?? "";

        /// <summary>
        /// Shifts <paramref name="code"/> so its least indented line starts at <paramref name="baseIndent"/>,
        /// keeping relative indentation. Used to drop model output into an indented selection.
        /// </summary>
        public static string Reindent(string code, string baseIndent, bool skipFirstLine)
        {
            if (string.IsNullOrEmpty(code))
                return code ?? "";
            baseIndent = baseIndent ?? "";

            string newLine = DetectNewLine(code);
            var lines = SplitLines(code);

            string common = null;
            foreach (var line in lines)
            {
                if (line.Trim().Length == 0)
                    continue;
                string indent = LeadingWhitespace(line);
                common = common == null ? indent : CommonPrefix(common, indent);
                if (common.Length == 0)
                    break;
            }
            common = common ?? "";

            if (common == baseIndent)
                return code;

            var sb = new StringBuilder(code.Length + lines.Count * baseIndent.Length);
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                    sb.Append(newLine);

                string line = lines[i];
                if (line.Trim().Length == 0)
                {
                    sb.Append(line.Length >= common.Length ? line.Substring(common.Length) : "");
                    continue;
                }

                string body = line.Substring(common.Length);
                if (!(i == 0 && skipFirstLine))
                    sb.Append(baseIndent);
                sb.Append(body);
            }
            return sb.ToString();
        }

        public static string LeadingWhitespace(string line)
        {
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
                i++;
            return line.Substring(0, i);
        }

        private static string CommonPrefix(string a, string b)
        {
            int n = Math.Min(a.Length, b.Length);
            int i = 0;
            while (i < n && a[i] == b[i])
                i++;
            return a.Substring(0, i);
        }

        public static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            var lines = SplitLines(text);
            if (lines.Count > 1 && lines[lines.Count - 1].Length == 0)
                return lines.Count - 1;
            return lines.Count;
        }
    }
}
