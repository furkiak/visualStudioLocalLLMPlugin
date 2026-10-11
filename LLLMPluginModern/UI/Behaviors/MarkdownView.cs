using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using LLLMPluginModern.Core.Text;

namespace LLLMPluginModern.UI.Behaviors
{
    /// <summary>
    /// Renders lightweight Markdown (headings, lists, quotes, bold, italic, inline code) into a
    /// read-only RichTextBox, so answers look polished but stay selectable for partial apply.
    /// </summary>
    public static class MarkdownView
    {
        public static readonly DependencyProperty MarkdownProperty = DependencyProperty.RegisterAttached(
            "Markdown", typeof(string), typeof(MarkdownView), new PropertyMetadata(null, OnMarkdownChanged));

        public static string GetMarkdown(DependencyObject element) => (string)element.GetValue(MarkdownProperty);

        public static void SetMarkdown(DependencyObject element, string value) => element.SetValue(MarkdownProperty, value);

        private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RichTextBox box)
                box.Document = Build(e.NewValue as string ?? "");
        }

        public static FlowDocument Build(string markdown)
        {
            var document = new FlowDocument { PagePadding = new Thickness(0) };
            var lines = TextTools.SplitLines(markdown);

            Paragraph paragraph = null;
            List list = null;
            MarkdownLineKind listKind = MarkdownLineKind.Blank;

            foreach (var raw in lines)
            {
                var kind = Markdown.ClassifyLine(raw, out string content, out int level);

                if (kind != MarkdownLineKind.Paragraph)
                    paragraph = null;
                if (kind != MarkdownLineKind.Bullet && kind != MarkdownLineKind.Numbered)
                {
                    list = null;
                    listKind = MarkdownLineKind.Blank;
                }

                switch (kind)
                {
                    case MarkdownLineKind.Blank:
                        break;

                    case MarkdownLineKind.Paragraph:
                        if (paragraph == null)
                        {
                            paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
                            document.Blocks.Add(paragraph);
                        }
                        else
                        {
                            paragraph.Inlines.Add(new LineBreak());
                        }
                        AddInlines(paragraph.Inlines, content);
                        break;

                    case MarkdownLineKind.Heading:
                        var heading = new Paragraph
                        {
                            Margin = new Thickness(0, 6, 0, 6),
                            FontWeight = FontWeights.SemiBold,
                            FontSize = level <= 1 ? 17 : level == 2 ? 15.5 : 14
                        };
                        AddInlines(heading.Inlines, content);
                        document.Blocks.Add(heading);
                        break;

                    case MarkdownLineKind.Bullet:
                    case MarkdownLineKind.Numbered:
                        if (list == null || listKind != kind)
                        {
                            list = new List
                            {
                                MarkerStyle = kind == MarkdownLineKind.Bullet ? TextMarkerStyle.Disc : TextMarkerStyle.None,
                                Margin = new Thickness(0, 0, 0, 8),
                                Padding = new Thickness(kind == MarkdownLineKind.Bullet ? 18 : 4, 0, 0, 0)
                            };
                            listKind = kind;
                            document.Blocks.Add(list);
                        }
                        var itemParagraph = new Paragraph { Margin = new Thickness(level * 16, 0, 0, 3) };
                        AddInlines(itemParagraph.Inlines, content);
                        list.ListItems.Add(new ListItem(itemParagraph));
                        break;

                    case MarkdownLineKind.Quote:
                        var quote = new Paragraph
                        {
                            Margin = new Thickness(0, 0, 0, 8),
                            Padding = new Thickness(10, 2, 0, 2),
                            BorderThickness = new Thickness(3, 0, 0, 0)
                        };
                        quote.SetResourceReference(Block.BorderBrushProperty, "Brush.Accent");
                        quote.SetResourceReference(TextElement.ForegroundProperty, "Brush.TextSecondary");
                        AddInlines(quote.Inlines, content);
                        document.Blocks.Add(quote);
                        break;

                    case MarkdownLineKind.Rule:
                        var rule = new Paragraph { Margin = new Thickness(0, 4, 0, 10), BorderThickness = new Thickness(0, 0, 0, 1), FontSize = 2 };
                        rule.SetResourceReference(Block.BorderBrushProperty, "Brush.Separator");
                        document.Blocks.Add(rule);
                        break;
                }
            }

            return document;
        }

        private static void AddInlines(InlineCollection inlines, string text)
        {
            foreach (var span in Markdown.ParseInline(text))
            {
                switch (span.Kind)
                {
                    case MarkdownSpanKind.Bold:
                        inlines.Add(new Bold(new Run(span.Text)));
                        break;
                    case MarkdownSpanKind.Italic:
                        inlines.Add(new Italic(new Run(span.Text)));
                        break;
                    case MarkdownSpanKind.Code:
                        var code = new Run(" " + span.Text + " ");
                        code.SetResourceReference(TextElement.FontFamilyProperty, "Font.Code");
                        code.SetResourceReference(TextElement.BackgroundProperty, "Brush.InlineCode");
                        code.SetResourceReference(TextElement.ForegroundProperty, "Brush.InlineCodeText");
                        code.FontSize = 12.5;
                        inlines.Add(code);
                        break;
                    default:
                        inlines.Add(new Run(span.Text));
                        break;
                }
            }
        }
    }
}
