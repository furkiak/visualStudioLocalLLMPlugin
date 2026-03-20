using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace LLLMPluginModern
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(LLLMPluginModernPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideBindingPath]
    public sealed class LLLMPluginModernPackage : AsyncPackage
    {
        public const string PackageGuidString = "769e069e-24ac-44f5-ab7c-6983dcc1e77a";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            try
            {
                Core.Bootstrapper.Initialize();

                await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                await Command1.InitializeAsync(this);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("A critical error occurred while loading the plugin:\n\n" + ex.Message,
                    "AI Assistant Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
    }
}