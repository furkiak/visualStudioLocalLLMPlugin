using System.Collections.Generic;
using System.Linq;
using System.Text;
using LLLMPluginModern.Core.Text;
using Xunit;

namespace LLLMPluginModern.Tests
{
    public class ThinkTagFilterTests
    {
        private static (string Answer, string Reasoning) Run(params string[] chunks)
        {
            var answer = new StringBuilder();
            var reasoning = new StringBuilder();
            var filter = new ThinkTagFilter();
            foreach (string chunk in chunks)
                filter.Process(chunk, a => answer.Append(a), r => reasoning.Append(r));
            filter.Flush(a => answer.Append(a), r => reasoning.Append(r));
            return (answer.ToString(), reasoning.ToString());
        }

        [Fact]
        public void Tags_split_across_streaming_chunks_are_recognised()
        {
            var (answer, reasoning) = Run("Hello <thi", "nk>reason", "ing</th", "ink> world");
            Assert.Equal("Hello  world", answer);
            Assert.Equal("reasoning", reasoning);
        }

        [Fact]
        public void Text_without_tags_is_all_answer()
        {
            Assert.Equal(("plain text", ""), Run("plain ", "text"));
        }

        [Fact]
        public void An_unclosed_think_block_stays_reasoning()
        {
            Assert.Equal(("", "still thinking"), Run("<think>still ", "thinking"));
        }

        [Fact]
        public void A_held_back_fragment_that_never_became_a_tag_is_returned_on_flush()
        {
            Assert.Equal(("Answer <th", ""), Run("Answer <th"));
        }
    }

    public class MarkdownBlockTests
    {
        [Fact]
        public void Text_and_fenced_code_are_split_into_blocks()
        {
            var blocks = Markdown.ParseBlocks("Intro\n```csharp\nvar x = 1;\n```\nOutro");

            Assert.Equal(3, blocks.Count);
            Assert.Equal(MarkdownBlockKind.Text, blocks[0].Kind);
            Assert.Equal("Intro", blocks[0].Content);
            Assert.Equal(MarkdownBlockKind.Code, blocks[1].Kind);
            Assert.Equal("var x = 1;", blocks[1].Content);
            Assert.Equal("csharp", blocks[1].Language);
            Assert.True(blocks[1].IsComplete);
            Assert.Equal("Outro", blocks[2].Content);
        }

        [Fact]
        public void A_fence_still_streaming_is_marked_incomplete()
        {
            var block = Assert.Single(Markdown.ParseBlocks("```python\nprint(1)"));
            Assert.Equal(MarkdownBlockKind.Code, block.Kind);
            Assert.Equal("print(1)", block.Content);
            Assert.False(block.IsComplete);
        }

        [Fact]
        public void A_longer_fence_can_contain_shorter_fences()
        {
            var block = Assert.Single(Markdown.ParseBlocks("````\n```\ninner\n```\n````"));
            Assert.Equal("```\ninner\n```", block.Content);
        }

        [Fact]
        public void A_fence_line_with_an_info_string_does_not_close_the_block()
        {
            var block = Assert.Single(Markdown.ParseBlocks("```\na\n```js\nb\n```"));
            Assert.Equal("a\n```js\nb", block.Content);
        }

        [Theory]
        [InlineData("```cs {title=x}\ncode\n```", "cs")]
        [InlineData("~~~ts\ncode\n~~~", "ts")]
        [InlineData("```\ncode\n```", "")]
        public void The_language_is_the_first_word_of_the_info_string(string markdown, string language)
        {
            var block = Assert.Single(Markdown.ParseBlocks(markdown));
            Assert.Equal("code", block.Content);
            Assert.Equal(language, block.Language);
        }

        [Fact]
        public void A_fence_indented_four_spaces_is_text()
        {
            var block = Assert.Single(Markdown.ParseBlocks("    ```\n    code"));
            Assert.Equal(MarkdownBlockKind.Text, block.Kind);
        }

        [Fact]
        public void ExtractCode_returns_only_the_code_blocks_when_there_are_any()
        {
            string code = Markdown.ExtractCode("Here:\n```cs\nint a;\n```\nand\n```cs\nint b;   \n```\n```\n   \n```", out bool containsCode);
            Assert.True(containsCode);
            Assert.Equal("int a;\n\nint b;", code);
        }

        [Fact]
        public void ExtractCode_falls_back_to_the_whole_answer()
        {
            Assert.Equal("Just words.", Markdown.ExtractCode("  Just words.\n", out bool containsCode));
            Assert.False(containsCode);
        }
    }

    public class MarkdownLineAndInlineTests
    {
        [Theory]
        [InlineData("## Title ##", MarkdownLineKind.Heading, "Title", 2)]
        [InlineData("#hashtag", MarkdownLineKind.Paragraph, "#hashtag", 0)]
        [InlineData("- item", MarkdownLineKind.Bullet, "item", 0)]
        [InlineData("    * nested", MarkdownLineKind.Bullet, "nested", 2)]
        [InlineData("12. step", MarkdownLineKind.Numbered, "12. step", 0)]
        [InlineData("3)   third", MarkdownLineKind.Numbered, "3) third", 0)]
        [InlineData("1.5 litres", MarkdownLineKind.Paragraph, "1.5 litres", 0)]
        [InlineData("> quoted", MarkdownLineKind.Quote, "quoted", 0)]
        [InlineData("---", MarkdownLineKind.Rule, "", 0)]
        [InlineData("- - -", MarkdownLineKind.Rule, "", 0)]
        [InlineData("   ", MarkdownLineKind.Blank, "   ", 0)]
        public void Lines_are_classified(string line, MarkdownLineKind kind, string content, int level)
        {
            Assert.Equal(kind, Markdown.ClassifyLine(line, out string actualContent, out int actualLevel));
            Assert.Equal(content, actualContent);
            Assert.Equal(level, actualLevel);
        }

        private static string Describe(string text) =>
            string.Join("|", Markdown.ParseInline(text).Select(s => s.Kind + ":" + s.Text));

        [Fact]
        public void Code_bold_and_italic_spans_are_found()
        {
            Assert.Equal("Text:Use |Code:var|Text: and |Bold:bold|Text: or |Italic:it", Describe("Use `var` and **bold** or *it*"));
        }

        [Theory]
        [InlineData("snake_case_name")]
        [InlineData("2 * 3 * 4")]
        [InlineData("a**")]
        public void Underscores_in_words_and_lone_asterisks_stay_plain(string text)
        {
            Assert.Equal("Text:" + text, Describe(text));
        }

        [Fact]
        public void Escapes_and_links_become_plain_text()
        {
            Assert.Equal("Text:*not italic*", Describe(@"\*not italic\*"));
            Assert.Equal("Text:see docs", Describe("see [docs](https://example.com)"));
        }

        [Fact]
        public void Double_backticks_can_contain_a_backtick()
        {
            Assert.Equal("Code:a ` b", Describe("``a ` b``"));
        }
    }

    public class TextToolsTests
    {
        [Theory]
        [InlineData("a\r\nb", "\r\n")]
        [InlineData("a\nb", "\n")]
        [InlineData("a\rb", "\r")]
        [InlineData("abc", "|")]
        public void The_line_break_style_is_detected(string text, string expected)
        {
            Assert.Equal(expected, TextTools.DetectNewLine(text, "|"));
        }

        [Fact]
        public void Lines_split_on_every_break_style()
        {
            Assert.Equal(new[] { "a", "b", "c", "" }, TextTools.SplitLines("a\r\nb\rc\n"));
        }

        [Fact]
        public void Duplicates_are_removed_but_blank_lines_and_the_trailing_break_are_kept()
        {
            Assert.Equal("a\r\nb\r\n\r\n\r\n", TextTools.RemoveDuplicateLines("a\r\nb\r\na\r\n\r\n\r\nb\r\n"));
        }

        [Fact]
        public void Replicate_emits_one_line_per_replacement()
        {
            Assert.Equal("var x = a;\nvar x = b;\nother\n",
                TextTools.ReplicateLines("var x = NAME;\nother\n", "NAME", new List<string> { "a", "b" }));
        }

        [Fact]
        public void Sorting_ignores_case_first_and_is_stable_for_ties()
        {
            Assert.Equal("A\na\nb\nC", TextTools.SortLines("b\nA\na\nC", descending: false));
            Assert.Equal("C\nb\na\nA", TextTools.SortLines("b\nA\na\nC", descending: true));
        }

        [Fact]
        public void The_other_line_tools_keep_the_trailing_break()
        {
            Assert.Equal("3\n2\n1\n", TextTools.ReverseLines("1\n2\n3\n"));
            Assert.Equal("a\nb", TextTools.RemoveEmptyLines("a\n\n  \nb"));
            Assert.Equal("a\nb\n", TextTools.TrimTrailingWhitespace("a  \nb\t\n"));
            Assert.Equal("// a\n// b", TextTools.AddToLines("a\nb", "// ", atLineStart: true));
            Assert.Equal("a;\nb;", TextTools.AddToLines("a\nb", ";", atLineStart: false));
            Assert.Equal(" bar ", TextTools.EraseText("foo bar foo", "foo"));
        }

        [Fact]
        public void Case_changes_ignore_the_current_culture()
        {
            Assert.Equal("ISTANBUL", TextTools.ToUpper("istanbul"));   // never the Turkish dotted İ
            Assert.Equal("", TextTools.ToLower(null));
        }

        [Fact]
        public void Reindent_moves_code_to_the_selection_indent_and_keeps_relative_indentation()
        {
            Assert.Equal("        if (x)\n        {\n            y();\n        }",
                TextTools.Reindent("if (x)\n{\n    y();\n}", "        ", skipFirstLine: false));
            Assert.Equal("\ta\n\t    b", TextTools.Reindent("    a\n        b", "\t", skipFirstLine: false));
            Assert.Equal("a\n  b", TextTools.Reindent("    a\n    b", "  ", skipFirstLine: true));
            Assert.Equal("a\n\nb", TextTools.Reindent("    a\n\n    b", "", skipFirstLine: false));
        }

        [Theory]
        [InlineData("a\nb\n", 2)]
        [InlineData("a\nb", 2)]
        [InlineData("", 0)]
        public void Lines_are_counted_without_the_trailing_break(string text, int expected)
        {
            Assert.Equal(expected, TextTools.CountLines(text));
        }
    }

    public class LanguageMapTests
    {
        [Theory]
        [InlineData(@"C:\src\Program.cs", "text", "C#", "csharp")]
        [InlineData("file.unknown", "CSharp", "C#", "csharp")]
        [InlineData("", "Rust Lang", "Rust Lang", "rustlang")]
        [InlineData("", "text", "Text", "")]
        [InlineData(null, null, "Text", "")]
        public void The_extension_wins_then_the_content_type(string path, string contentType, string name, string id)
        {
            Assert.Equal((name, id), LanguageMap.Resolve(path, contentType));
        }

        [Theory]
        [InlineData("cs", "C#")]
        [InlineData("  py ", "Python")]
        [InlineData("", "Code")]
        [InlineData("zig", "zig")]
        public void Fence_ids_get_display_names(string id, string expected)
        {
            Assert.Equal(expected, LanguageMap.DisplayNameForFence(id));
        }
    }
}
