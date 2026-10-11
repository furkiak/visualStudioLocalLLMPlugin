using System;
using System.Windows;
using System.Windows.Interop;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace LLLMPluginModern.VisualStudio
{
    internal static class VsHost
    {
        /// <summary>Shows a WPF window as a proper Visual Studio modal dialog (owned by the IDE, other windows disabled).</summary>
        public static bool? ShowModal(Window window)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var shell = Package.GetGlobalService(typeof(SVsUIShell)) as IVsUIShell;
            IntPtr owner = IntPtr.Zero;
            if (shell != null && ErrorHandler.Succeeded(shell.GetDialogOwnerHwnd(out owner)) && owner != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = owner;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            shell?.EnableModeless(0);
            try
            {
                return window.ShowDialog();
            }
            finally
            {
                shell?.EnableModeless(1);
            }
        }

        /// <summary>True when the current Visual Studio color theme is dark.</summary>
        public static bool IsDarkTheme()
        {
            try
            {
                var color = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
                return (color.R * 299 + color.G * 587 + color.B * 114) / 1000 < 128;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
