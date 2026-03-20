namespace LLLMPluginModern.Core.Interfaces
{
    public interface IDialogService
    {
        void ShowSettings();
        bool ShowInput(string title, string okButtonText, out string result);
        void ShowMessage(string message, string title = "Information");
        bool ShowModifyReplicate(out string targetWord, out string[] replacementWords);
        bool ShowAdd(out string textToAdd, out bool isLeft);
    }
}