using System;
using System.IO;

namespace LLLMPluginModern.Core.Models
{
    /// <summary>Snapshot of the editor state captured when the assistant is opened.</summary>
    public sealed class EditorContext
    {
        public static EditorContext Empty { get; } = new EditorContext();

        /// <summary>True when an editable text view was found and edits can be applied to it.</summary>
        public bool IsAvailable { get; set; }

        public string SelectedText { get; set; } = "";
        public string FilePath { get; set; } = "";

        /// <summary>Human readable language name, e.g. "C#".</summary>
        public string LanguageName { get; set; } = "";

        /// <summary>Markdown fence identifier, e.g. "csharp".</summary>
        public string LanguageId { get; set; } = "";

        /// <summary>Line break used by the document.</summary>
        public string NewLine { get; set; } = Environment.NewLine;

        /// <summary>Leading whitespace of the line where the selection starts.</summary>
        public string BaseIndent { get; set; } = "";

        /// <summary>True when the selection does not start at the beginning of its line,
        /// so the first inserted line must not be indented again.</summary>
        public bool StartsMidLine { get; set; }

        /// <summary>Lazily reads the full document text (used for optional file context).</summary>
        public Func<string> DocumentTextProvider { get; set; }

        /// <summary>Opaque host specific state (tracking spans etc.).</summary>
        public object Handle { get; set; }

        public bool HasSelection => !string.IsNullOrEmpty(SelectedText);

        public string FileName => string.IsNullOrEmpty(FilePath) ? "" : Path.GetFileName(FilePath);
    }

    public enum ApplyMode
    {
        ReplaceSelection,
        InsertBelow
    }

    public sealed class ApplyRequest
    {
        public ApplyRequest(string text, ApplyMode mode, bool isCode)
        {
            Text = text ?? "";
            Mode = mode;
            IsCode = isCode;
        }

        public string Text { get; }
        public ApplyMode Mode { get; }

        /// <summary>Code produced by the model (re-indented on apply); false for text-tool output.</summary>
        public bool IsCode { get; }
    }
}
