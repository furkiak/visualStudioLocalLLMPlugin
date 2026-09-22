using System;
using System.Linq;

namespace LLLMPluginModern.Core.Models
{
    public sealed class SelectionChange
    {
        private readonly string document, contents, selection;
        private readonly int start, end;
        public SelectionChange(string document, string contents, string selection, int start, int end)
        { this.document = document; this.contents = contents; this.selection = selection; this.start = start; this.end = end; }
        public bool Matches(string currentDocument, string currentContents, string currentSelection, int currentStart, int currentEnd)
        {
            return string.Equals(document, currentDocument, StringComparison.Ordinal) && contents == currentContents &&
                selection == currentSelection && start == currentStart && end == currentEnd;
        }
        public static string Preview(string before, string after)
        {
            if (string.IsNullOrEmpty(after)) return "No candidate yet.";
            var a = (before ?? "").Replace("\r\n", "\n").Split('\n');
            var b = after.Replace("\r\n", "\n").Split('\n');
            int prefix = 0, suffix = 0;
            while (prefix < Math.Min(a.Length, b.Length) && a[prefix] == b[prefix]) prefix++;
            while (suffix < Math.Min(a.Length, b.Length) - prefix && a[a.Length - 1 - suffix] == b[b.Length - 1 - suffix]) suffix++;
            if (prefix == a.Length && prefix == b.Length) return "No line-content changes (line endings may differ).";
            var removed = a.Skip(prefix).Take(a.Length - prefix - suffix).Select(x => "- " + x);
            var added = b.Skip(prefix).Take(b.Length - prefix - suffix).Select(x => "+ " + x);
            var changes = removed.Concat(added).ToArray();
            return "Selection-relative change from line " + (prefix + 1) + "\n" + string.Join("\n", changes.Take(2000)) +
                (changes.Length > 2000 ? "\n… Preview truncated. Review the full Candidate tab before applying." : "");
        }
    }
}
