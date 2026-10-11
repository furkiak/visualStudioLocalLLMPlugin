using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.UI.Views;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace LLLMPluginModern.VisualStudio
{
    /// <summary>Tools ▸ AI Assistant, the editor context menu entry and Ctrl+1.</summary>
    internal sealed class OpenAssistantCommand
    {
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid("B51F9B79-13A2-4FA8-88D1-9BCCF6DE418D");

        private readonly AsyncPackage _package;
        private readonly AppServices _services;

        private OpenAssistantCommand(AsyncPackage package, AppServices services, OleMenuCommandService commandService)
        {
            _package = package;
            _services = services;
            commandService.AddCommand(new OleMenuCommand(Execute, new CommandID(CommandSet, CommandId)));
        }

        public static async Task InitializeAsync(AsyncPackage package, AppServices services)
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService
                                 ?? throw new InvalidOperationException("The menu command service is not available.");
            _ = new OpenAssistantCommand(package, services, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                EditorContext context = _services.Editor.GetActiveContext();
                var viewModel = _services.CreateMainViewModel(context);
                var view = new MainView(viewModel);

                if (VsHost.ShowModal(view) != true || viewModel.PendingApply == null)
                    return;

                ApplyRequest request = viewModel.PendingApply;
                if (!context.IsAvailable)
                {
                    _services.Clipboard.SetText(request.Text);
                    ShowMessage("No editable document was open, so the result was copied to the clipboard.", OLEMSGICON.OLEMSGICON_INFO);
                    return;
                }

                bool matchIndentation = _services.Settings.Load().MatchIndentation;
                string error = _services.Editor.Apply(context, request, matchIndentation);
                if (error != null)
                {
                    _services.Clipboard.SetText(request.Text);
                    ShowMessage(error + "\n\nThe result was copied to the clipboard instead.", OLEMSGICON.OLEMSGICON_WARNING);
                }
            }
            catch (Exception ex)
            {
                ActivityLog.LogError("LLLMPluginModern", ex.ToString());
                ShowMessage("Something went wrong:\n\n" + ex.Message, OLEMSGICON.OLEMSGICON_CRITICAL);
            }
        }

        private void ShowMessage(string message, OLEMSGICON icon)
        {
            VsShellUtilities.ShowMessageBox(_package, message, "AI Assistant", icon, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
