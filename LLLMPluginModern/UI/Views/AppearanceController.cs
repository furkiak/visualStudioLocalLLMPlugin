using System.Windows;
using LLLMPluginModern.Core.Models;

namespace LLLMPluginModern.UI.Views
{
    public static class AppearanceController
    {
        public static void Apply(Window window, int textScalePercent)
        {
            double fontSize = AppearancePreferences.BodyFontSize(textScalePercent);
            window.Resources["PluginBodyFontSize"] = fontSize;
            window.Resources["PluginCaptionFontSize"] = 12d * textScalePercent / 100d;
            window.Resources["PluginHeadingFontSize"] = 20d * textScalePercent / 100d;
        }
    }
}
