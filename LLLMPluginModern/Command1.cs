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
                string selectedText = "";

                if (dte != null && dte.ActiveDocument != null && dte.ActiveDocument.Selection != null)
                {
                    TextSelection selection = (TextSelection)dte.ActiveDocument.Selection;
                    selectedText = selection.Text ?? "";
                }

                var mainViewModel = Core.Bootstrapper.ServiceProvider.GetService(typeof(UI.ViewModels.MainViewModel)) as UI.ViewModels.MainViewModel;

                if (mainViewModel == null)
                {
                    VsShellUtilities.ShowMessageBox(this.package, "DI Error: ViewModel could not be loaded.", "Error", OLEMSGICON.OLEMSGICON_CRITICAL, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                mainViewModel.SelectedText = selectedText;
                var mainView = new UI.Views.MainView(mainViewModel);
                bool? result = mainView.ShowDialog();

                if (result == true && !string.IsNullOrWhiteSpace(mainViewModel.ResponseText))
                {
                    if (dte != null && dte.ActiveDocument != null && dte.ActiveDocument.Selection != null)
                    {
                        bool isUndoContextOpen = false;
                        try
                        {
                         
                            dte.UndoContext.Open("Apply AI Text", false);
                            isUndoContextOpen = true;

                            TextSelection selection = (TextSelection)dte.ActiveDocument.Selection;
                            selection.Text = mainViewModel.ResponseText;
                        }
                        finally
                        {
                        
                            if (isUndoContextOpen)
                            {
                                dte.UndoContext.Close();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                VsShellUtilities.ShowMessageBox(this.package, "Error:\n" + ex.Message + "\n\n" + ex.StackTrace, "Plugin Crashed!", OLEMSGICON.OLEMSGICON_CRITICAL, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }
    }
}