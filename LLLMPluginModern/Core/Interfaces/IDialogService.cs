namespace LLLMPluginModern.Core.Interfaces
{
    public interface IDialogService
    {
        /// <summary>Shows the settings window. Returns true when the user saved changes.</summary>
        bool ShowSettings();

        bool ShowInput(string title, string message, string okButtonText, out string result);

        void ShowMessage(string message, string title = "Information");

        void ShowAbout();

        bool ShowModifyReplicate(out string targetWord, out string[] replacementWords);

        bool ShowAdd(out string textToAdd, out bool atLineStart);
    }

    public interface IClipboardService
    {
        void SetText(string text);
    }

    public interface IEditorService
    {
        /// <summary>Captures the active document, selection and language. Must be called on the UI thread.</summary>
        Models.EditorContext GetActiveContext();

        /// <summary>Applies text to the document captured in <paramref name="context"/> as a single undo step.</summary>
        /// <returns>null on success, otherwise an error message for the user.</returns>
        string Apply(Models.EditorContext context, Models.ApplyRequest request, bool matchIndentation);
    }
}
