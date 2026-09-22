using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace LLLMPluginModern
{
    internal sealed class Command1
    {
        public const int CommandId = 0x0100;

     
        public static readonly Guid CommandSet = new Guid("B51F9B79-13A2-4FA8-88D1-9BCCF6DE418D");

        private readonly AsyncPackage package;

        private Command1(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        public static Command1 Instance { get; private set; }

        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider => this.package;

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService == null)
                throw new Exception("MenuCommandService could not be retrieved");
            Instance = new Command1(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                DTE2 dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
                var document = dte?.ActiveDocument;
                var selection = document?.Selection as TextSelection;
                var textDocument = document?.Object("TextDocument") as TextDocument;
                if (selection == null || textDocument == null || string.IsNullOrEmpty(selection.Text))
                {
                    VsShellUtilities.ShowMessageBox(package, "Select text in an editable document first.", "Local LLM tools", OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }
                string selectedText = selection.Text;
                var captured = new Core.Models.SelectionChange(document.FullName,
                    textDocument.StartPoint.CreateEditPoint().GetText(textDocument.EndPoint), selectedText,
                    selection.TopPoint.AbsoluteCharOffset, selection.BottomPoint.AbsoluteCharOffset);
                Func<bool> targetCurrent = () =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    try
                    {
                        var activeDocument = dte.ActiveDocument;
                        if (activeDocument == null || !string.Equals(activeDocument.FullName, document.FullName, StringComparison.OrdinalIgnoreCase)) return false;
                        var current = document.Selection as TextSelection;
                        return current != null && captured.Matches(document.FullName,
                            textDocument.StartPoint.CreateEditPoint().GetText(textDocument.EndPoint), current.Text,
                            current.TopPoint.AbsoluteCharOffset, current.BottomPoint.AbsoluteCharOffset);
                    }
                    catch (Exception) { return false; }
                };

                var mainViewModel = Core.Bootstrapper.ServiceProvider.GetService(typeof(UI.ViewModels.MainViewModel)) as UI.ViewModels.MainViewModel;

                if (mainViewModel == null)
                {
                    VsShellUtilities.ShowMessageBox(this.package, "DI Error: ViewModel could not be loaded.", "Error", OLEMSGICON.OLEMSGICON_CRITICAL, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                mainViewModel.SelectedText = selectedText;
                mainViewModel.IsTargetCurrent = targetCurrent;
                var mainView = new UI.Views.MainView(mainViewModel);
                bool? result = mainView.ShowDialog();

                if (result == true)
                {
                    if (mainViewModel.IsBusy || !mainViewModel.Reviewed || string.IsNullOrWhiteSpace(mainViewModel.ResponseText) || !targetCurrent())
                    {
                        VsShellUtilities.ShowMessageBox(package, "The document or selection changed. Select the code again and regenerate; nothing was applied.", "Selection changed", OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                        return;
                    }
                    if (dte.UndoContext.IsOpen)
                    {
                        VsShellUtilities.ShowMessageBox(package, "Another editor operation is active. Finish it before applying.", "Editor busy", OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                        return;
                    }
                    bool undoOpened = false;
                    try
                    {
                        dte.UndoContext.Open("Apply reviewed LLM candidate", false);
                        undoOpened = true;
                        selection.Text = mainViewModel.ResponseText;
                    }
                    catch
                    {
                        if (undoOpened) { dte.UndoContext.SetAborted(); undoOpened = false; }
                        throw;
                    }
                    finally { if (undoOpened) dte.UndoContext.Close(); }
                }
            }
            catch (Exception)
            {
                VsShellUtilities.ShowMessageBox(this.package, "The editor operation failed. Check that the document is writable and try again.", "Local LLM tools", OLEMSGICON.OLEMSGICON_CRITICAL, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }
    }
}
