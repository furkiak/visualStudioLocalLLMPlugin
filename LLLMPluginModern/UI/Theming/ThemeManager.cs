using System;
using System.Collections.Generic;
using System.Windows;
using LLLMPluginModern.Core.Models;

namespace LLLMPluginModern.UI.Theming
{
    /// <summary>
    /// Loads the Liquid Glass resource dictionaries into each window. The palette follows the
    /// Visual Studio theme ("Auto") unless the user picked Dark or Light explicitly.
    /// </summary>
    public static class ThemeManager
    {
        private static readonly Dictionary<string, ResourceDictionary> Cache = new Dictionary<string, ResourceDictionary>();
        private static Func<AppSettings> _settings = () => new AppSettings();
        private static Func<bool> _hostIsDark = () => true;

        public static void Configure(Func<AppSettings> settings, Func<bool> hostIsDark)
        {
            _settings = settings ?? _settings;
            _hostIsDark = hostIsDark ?? _hostIsDark;
        }

        public static bool IsDark
        {
            get
            {
                string mode = SafeSettings().Theme;
                if (mode == ThemeModes.Dark)
                    return true;
                if (mode == ThemeModes.Light)
                    return false;
                try
                {
                    return _hostIsDark();
                }
                catch (Exception)
                {
                    return true;
                }
            }
        }

        public static bool UseTranslucency => SafeSettings().UseTranslucency;

        /// <summary>Adds (or replaces) the theme dictionaries of <paramref name="element"/>.</summary>
        public static void Apply(FrameworkElement element, bool? dark = null)
        {
            bool isDark = dark ?? IsDark;
            var merged = element.Resources.MergedDictionaries;

            var palette = Load(isDark ? "Dark" : "Light");
            var others = new[] { Load(isDark ? "Light" : "Dark") };
            foreach (var stale in others)
                merged.Remove(stale);
            if (!merged.Contains(palette))
                merged.Insert(0, palette);

            foreach (var name in new[] { "Icons", "Controls" })
            {
                var dictionary = Load(name);
                if (!merged.Contains(dictionary))
                    merged.Add(dictionary);
            }
        }

        /// <summary>Looks up an icon geometry by key (icons are theme independent).</summary>
        public static object FindIcon(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            var icons = Load("Icons");
            return icons.Contains(key) ? icons[key] : null;
        }

        private static ResourceDictionary Load(string name)
        {
            if (!Cache.TryGetValue(name, out var dictionary))
            {
                string assembly = typeof(ThemeManager).Assembly.GetName().Name;
                dictionary = new ResourceDictionary
                {
                    Source = new Uri($"pack://application:,,,/{assembly};component/UI/Themes/{name}.xaml", UriKind.Absolute)
                };
                Cache[name] = dictionary;
            }
            return dictionary;
        }

        private static AppSettings SafeSettings()
        {
            try
            {
                return _settings() ?? new AppSettings();
            }
            catch (Exception)
            {
                return new AppSettings();
            }
        }
    }
}
