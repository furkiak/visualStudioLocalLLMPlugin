using System;
using System.Collections.Generic;
using System.Text;
using LLLMPluginModern.Core.Models;

namespace LLLMPluginModern.Core.Prompts
{
    public sealed class QuickAction
    {
        public QuickAction(string id, string title, string iconKey, string prompt, string tooltip)
        {
            Id = id;
            Title = title;
            IconKey = iconKey;
            Prompt = prompt;
            ToolTip = tooltip;
        }

        public string Id { get; }
        public string Title { get; }

        /// <summary>Resource key of the icon geometry.</summary>
        public string IconKey { get; }

        public string Prompt { get; }
        public string ToolTip { get; }

        public static IReadOnlyList<QuickAction> Defaults { get; } = new[]
        {
            new QuickAction("explain", "Explain", "Icon.Book",
                "Explain what this code does, step by step. Point out anything surprising or risky.",
                "Explain the selected code"),
            new QuickAction("refactor", "Refactor", "Icon.Wand",
                "Refactor this code for readability and maintainability without changing its behavior. Return the complete updated code.",
                "Refactor without changing behavior"),
            new QuickAction("fix", "Fix bugs", "Icon.Bug",
                "Find and fix the bugs in this code. Return the complete corrected code, then list each fix in one short line.",
                "Find and fix bugs"),
            new QuickAction("optimize", "Optimize", "Icon.Bolt",
                "Optimize this code for performance and memory use without changing its behavior. Return the complete updated code and summarize the gains.",
                "Make it faster and leaner"),
            new QuickAction("document", "Document", "Icon.Comment",
                "Add clear documentation comments in the idiomatic style for this language (for example XML doc comments in C#) and concise inline comments where they add value. Return the complete updated code.",
                "Add documentation comments"),
            new QuickAction("tests", "Unit tests", "Icon.Flask",
                "Write thorough unit tests for this code using the most common test framework for this language (for example xUnit for C#). Cover edge cases. Return only the test code.",
                "Generate unit tests"),
            new QuickAction("review", "Review", "Icon.Shield",
                "Review this code like a senior engineer. List bugs, security issues, performance problems and maintainability concerns, most severe first, each with a concrete fix.",
                "Code review"),
        };
    }

    public static class PromptBuilder
    {
        public const int MaxFileContextChars = 120_000;

        public const string DefaultSystemPrompt =
            "You are an expert software engineer working inside Visual Studio.\n" +
            "- Be concise and precise; skip filler.\n" +
            "- When you return code, put it in a fenced code block with the correct language tag.\n" +
            "- When asked to change the provided code, return the complete updated snippet (not a diff), " +
            "keep the original style and indentation, and keep explanations outside the code block.\n" +
            "- Do not invent APIs. If something is ambiguous, state your assumption briefly.";

        public static string BuildSystemPrompt(string customPrompt, EditorContext context, bool includeFileContext, out bool fileContextTruncated)
        {
            fileContextTruncated = false;
            var sb = new StringBuilder(string.IsNullOrWhiteSpace(customPrompt) ? DefaultSystemPrompt : customPrompt.Trim());

            if (context != null && !string.IsNullOrEmpty(context.FileName))
            {
                sb.Append("\n\nThe user is editing ").Append(context.FileName);
                if (!string.IsNullOrEmpty(context.LanguageName) && context.LanguageName != "Text")
                    sb.Append(" (").Append(context.LanguageName).Append(')');
                sb.Append('.');
            }

            if (includeFileContext && context?.DocumentTextProvider != null)
            {
                string document = SafeRead(context.DocumentTextProvider);
                if (!string.IsNullOrEmpty(document))
                {
                    if (document.Length > MaxFileContextChars)
                    {
                        document = document.Substring(0, MaxFileContextChars);
                        fileContextTruncated = true;
                    }
                    string fence = Fence(document);
                    sb.Append("\n\nFull content of the current file, for reference")
                      .Append(fileContextTruncated ? " (truncated)" : "")
                      .Append(":\n").Append(fence).Append(context.LanguageId).Append('\n')
                      .Append(document).Append('\n').Append(fence);
                }
            }

            return sb.ToString();
        }

        /// <summary>Builds the user turn. Code is attached when it is new or changed since the last turn.</summary>
        public static string BuildUserMessage(string prompt, string code, EditorContext context, bool attachCode)
        {
            prompt = (prompt ?? "").Trim();
            if (!attachCode || string.IsNullOrWhiteSpace(code))
                return prompt;

            string fence = Fence(code);
            var sb = new StringBuilder();
            sb.Append(fence).Append(context?.LanguageId ?? "").Append('\n')
              .Append(code.TrimEnd('\r', '\n'))
              .Append('\n').Append(fence);
            if (prompt.Length > 0)
                sb.Append("\n\n").Append(prompt);
            return sb.ToString();
        }

        /// <summary>A backtick fence longer than any backtick run inside <paramref name="content"/>.</summary>
        private static string Fence(string content)
        {
            int longest = 0, run = 0;
            foreach (char c in content ?? "")
            {
                run = c == '`' ? run + 1 : 0;
                if (run > longest)
                    longest = run;
            }
            return new string('`', Math.Max(3, longest + 1));
        }

        private static string SafeRead(Func<string> provider)
        {
            try
            {
                return provider();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
