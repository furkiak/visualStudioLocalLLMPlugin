using System;
using System.Runtime.InteropServices;
using System.Threading;
using LLLMPluginModern.UI.Theming;
using LLLMPluginModern.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace LLLMPluginModern
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideBindingPath]
    public sealed class LLLMPluginModernPackage : AsyncPackage
    {
        public const string PackageGuidString = "769e069e-24ac-44f5-ab7c-6983dcc1e77a";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            try
            {
                var editor = await VsEditorService.CreateAsync(this);
                var services = AppServices.CreateDefault(editor);
                ThemeManager.Configure(() => services.Settings.Load(), VsHost.IsDarkTheme);
                await OpenAssistantCommand.InitializeAsync(this, services);
            }
            catch (Exception ex)
            {
                ActivityLog.LogError("LLLMPluginModern", ex.ToString());
                VsShellUtilities.ShowMessageBox(this,
                    "The AI Assistant extension could not be loaded:\n\n" + ex.Message,
                    "AI Assistant",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }
    }
}
