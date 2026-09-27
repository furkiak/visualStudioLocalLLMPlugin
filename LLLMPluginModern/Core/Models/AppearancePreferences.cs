using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace LLLMPluginModern.Core.Models
{
    public static class AppearancePreferences
    {
        public static void Validate(int textScalePercent)
        {
            if (textScalePercent != 100 && textScalePercent != 150 && textScalePercent != 200)
                throw new ArgumentException("Text size must be 100%, 150% or 200%.");
        }

        public static int ReadScale(JObject saved)
        {
            var token = saved["TextScalePercent"];
            if (token == null) return 100;
            if (token.Type != JTokenType.Integer) throw new InvalidDataException("Unsupported saved text size.");
            int scale;
            try { scale = token.Value<int>(); }
            catch (OverflowException) { throw new InvalidDataException("Unsupported saved text size."); }
            Validate(scale);
            return scale;
        }

        public static double BodyFontSize(int textScalePercent)
        {
            Validate(textScalePercent);
            return 14d * textScalePercent / 100d;
        }
    }
}
