using System;
using System.Linq;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Core.Prompts;
using LLLMPluginModern.Infrastructure.Http;
using Xunit;

namespace LLLMPluginModern.Tests
{
    public class PromptBuilderTests
    {
        private static EditorContext CSharpFile(Func<string> document = null) => new EditorContext
        {
            FilePath = @"C:\src\Program.cs",
            LanguageName = "C#",
            LanguageId = "csharp",
            DocumentTextProvider = document
        };

        [Fact]
        public void The_selection_is_attached_as_a_fenced_block_before_the_question()
        {
            Assert.Equal("```csharp\nint a;\n```\n\nExplain", PromptBuilder.BuildUserMessage(" Explain ", "int a;\r\n", CSharpFile(), attachCode: true));
            Assert.Equal("Explain", PromptBuilder.BuildUserMessage("Explain", "int a;", CSharpFile(), attachCode: false));
        }

        [Fact]
        public void The_fence_is_longer_than_any_backtick_run_in_the_code()
        {
            string message = PromptBuilder.BuildUserMessage("", "var md = \"```\";", CSharpFile(), attachCode: true);
            Assert.StartsWith("````csharp\n", message);
            Assert.EndsWith("\n````", message);
        }

        [Fact]
        public void The_system_prompt_names_the_file_and_can_include_it()
        {
            string prompt = PromptBuilder.BuildSystemPrompt(null, CSharpFile(() => "class P {}"), includeFileContext: true, out bool truncated);

            Assert.StartsWith(PromptBuilder.DefaultSystemPrompt, prompt);
            Assert.Contains("The user is editing Program.cs (C#).", prompt);
            Assert.Contains("```csharp\nclass P {}\n```", prompt);
            Assert.False(truncated);
        }

        [Fact]
        public void A_large_file_is_truncated_and_says_so()
        {
            string prompt = PromptBuilder.BuildSystemPrompt(null, CSharpFile(() => new string('x', PromptBuilder.MaxFileContextChars + 10)), true, out bool truncated);
            Assert.True(truncated);
            Assert.Contains("(truncated)", prompt);
            Assert.Contains(new string('x', PromptBuilder.MaxFileContextChars) + "\n```", prompt);
        }

        [Fact]
        public void A_failing_document_reader_only_drops_the_file_context()
        {
            string prompt = PromptBuilder.BuildSystemPrompt(null, CSharpFile(() => throw new InvalidOperationException("closed")), true, out bool truncated);
            Assert.DoesNotContain("Full content", prompt);
            Assert.False(truncated);
        }

        [Fact]
        public void A_custom_prompt_replaces_the_default_and_plain_text_files_get_no_language()
        {
            var notes = new EditorContext { FilePath = @"C:\notes.txt", LanguageName = "Text" };
            string prompt = PromptBuilder.BuildSystemPrompt("  Be brief.  ", notes, includeFileContext: false, out _);
            Assert.Equal("Be brief.\n\nThe user is editing notes.txt.", prompt);
        }

        [Fact]
        public void Quick_actions_have_unique_ids()
        {
            var ids = QuickAction.Defaults.Select(a => a.Id).ToList();
            Assert.Equal(7, ids.Count);
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.All(QuickAction.Defaults, a => Assert.False(string.IsNullOrWhiteSpace(a.Prompt)));
        }
    }

    public class EndpointTests
    {
        [Theory]
        [InlineData("http://localhost:11434/api/generate", "http://localhost:11434")]
        [InlineData("http://localhost:11434/api/chat/?stream=true", "http://localhost:11434")]
        [InlineData("  http://gpu-box:11434/  ", "http://gpu-box:11434")]
        [InlineData("", "http://localhost:11434")]
        [InlineData(null, "http://localhost:11434")]
        [InlineData("http://h/api/chat/api/generate", "http://h")]
        public void Ollama_urls_are_reduced_to_their_base(string typed, string expected)
        {
            Assert.Equal(expected, Endpoints.NormalizeBase(typed, "http://localhost:11434", "/api/generate", "/api/chat"));
        }

        [Fact]
        public void OpenAI_style_endpoints_keep_their_version_segment()
        {
            Assert.Equal("https://api.openai.com/v1", Endpoints.NormalizeBase("https://api.openai.com/v1/chat/completions/", "x", "/chat/completions"));
        }

        [Fact]
        public void Relative_paths_are_joined_with_exactly_one_slash()
        {
            Assert.Equal("http://localhost:11434/api/tags", Endpoints.Combine("http://localhost:11434/", "/api/tags").ToString());
        }

        [Theory]
        [InlineData("localhost:11434")]
        [InlineData("ftp://example.com")]
        [InlineData("not a url")]
        public void Anything_but_http_or_https_is_rejected_with_a_readable_message(string baseUrl)
        {
            var error = Assert.Throws<LlmApiException>(() => Endpoints.Combine(baseUrl, "api/tags"));
            Assert.Contains("Settings", error.Message);
        }
    }
}
