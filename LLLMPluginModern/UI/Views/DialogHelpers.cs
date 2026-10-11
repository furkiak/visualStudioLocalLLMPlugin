using System;
using System.Windows;

namespace LLLMPluginModern.UI.Views
{
    internal static class DialogHelpers
    {
        /// <summary>Closes a dialog with a result; falls back to Close() if the window was not shown modally.</summary>
        public static void CloseWithResult(Window window, bool ok)
        {
            try
            {
                window.DialogResult = ok;
            }
            catch (InvalidOperationException)
            {
                window.Close();
            }
        }
    }
}
