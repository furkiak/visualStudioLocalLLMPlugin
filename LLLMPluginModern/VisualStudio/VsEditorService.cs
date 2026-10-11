using System;
using System.Linq;
using System.Threading.Tasks;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Core.Text;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;

namespace LLLMPluginModern.VisualStudio
{
    /// <summary>
    /// Reads and edits the active document through the modern editor API (ITextBuffer edits):
    /// atomic, a single undo step, no auto-formatting side effects, and it works with any
    /// language including projection buffers (Razor, XAML designers' code view…).
    /// </summary>
    internal sealed class VsEditorService : IEditorService
    {
        private readonly IVsTextManager _textManager;
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private readonly ITextUndoHistoryRegistry _undoRegistry;

        private VsEditorService(IVsTextManager textManager, IVsEditorAdaptersFactoryService adapters, ITextUndoHistoryRegistry undoRegistry)
        {
            _textManager = textManager;
            _adapters = adapters;
            _undoRegistry = undoRegistry;
        }

        public static async Task<VsEditorService> CreateAsync(AsyncPackage package)
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var textManager = await package.GetServiceAsync(typeof(SVsTextManager)) as IVsTextManager;
            var componentModel = await package.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            return new VsEditorService(
                textManager,
                componentModel?.GetService<IVsEditorAdaptersFactoryService>(),
                componentModel?.GetService<ITextUndoHistoryRegistry>());
        }

        private sealed class EditorHandle
        {
            public IWpfTextView View;
            public ITrackingSpan Span;
            public ITrackingSpan[] BoxSpans;
            public bool ReplaceWholeLine;
            public bool SelectionEndsWithLineBreak;
        }

        public EditorContext GetActiveContext()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IWpfTextView view = GetActiveView();
            if (view == null || view.IsClosed)
                return new EditorContext();

            ITextSnapshot snapshot = view.TextSnapshot;
            ITextBuffer documentBuffer = view.TextDataModel?.DocumentBuffer ?? view.TextBuffer;
            string filePath = GetFilePath(documentBuffer) ?? GetFilePath(view.TextBuffer) ?? "";
            var language = LanguageMap.Resolve(filePath, documentBuffer.ContentType?.TypeName);
            var handle = new EditorHandle { View = view };

            string selectedText;
            SnapshotSpan span;
            string baseIndent;
            bool startsMidLine;

            var selection = view.Selection;
            if (selection.IsEmpty)
            {
                var caret = view.Caret.Position.VirtualBufferPosition;
                var line = caret.Position.GetContainingLine();
                string lineText = line.GetText();
                selectedText = "";

                if (lineText.Trim().Length == 0)
                {
                    // Blank line: replace it entirely and indent like the caret column suggests.
                    span = new SnapshotSpan(line.Start, line.End);
                    baseIndent = caret.IsInVirtualSpace ? lineText + new string(' ', caret.VirtualSpaces) : lineText;
                    startsMidLine = false;
                    handle.ReplaceWholeLine = true;
                }
                else
                {
                    span = new SnapshotSpan(caret.Position, 0);
                    baseIndent = TextTools.LeadingWhitespace(lineText);
                    startsMidLine = caret.Position > line.Start;
                }
            }
            else if (selection.Mode == TextSelectionMode.Box && selection.SelectedSpans.Count > 1)
            {
                var spans = selection.SelectedSpans;
                span = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End);
                selectedText = string.Join(GetNewLine(view, snapshot, span.Start), spans.Select(s => s.GetText()));
                handle.BoxSpans = spans.Select(s => snapshot.CreateTrackingSpan(s, SpanTrackingMode.EdgeInclusive)).ToArray();
                baseIndent = "";
                startsMidLine = true;
            }
            else
            {
                span = selection.StreamSelectionSpan.SnapshotSpan;
                var startLine = span.Start.GetContainingLine();
                string lead = TextTools.LeadingWhitespace(startLine.GetText());

                // A selection that starts inside the indentation is widened to the line start so the
                // model sees (and we can restore) the real indentation.
                if (span.Start.Position <= startLine.Start.Position + lead.Length)
                    span = new SnapshotSpan(startLine.Start, span.End);

                selectedText = span.GetText();
                baseIndent = lead;
                startsMidLine = span.Start > startLine.Start;
                handle.SelectionEndsWithLineBreak = selectedText.EndsWith("\n", StringComparison.Ordinal) || selectedText.EndsWith("\r", StringComparison.Ordinal);
            }

            handle.Span = snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);
            ITextSnapshot documentSnapshot = documentBuffer.CurrentSnapshot;

            return new EditorContext
            {
                IsAvailable = true,
                SelectedText = selectedText,
                FilePath = filePath,
                LanguageName = language.Name,
                LanguageId = language.Id,
                NewLine = GetNewLine(view, snapshot, span.Start),
                BaseIndent = baseIndent,
                StartsMidLine = startsMidLine,
                DocumentTextProvider = () => documentSnapshot.GetText(),
                Handle = handle
            };
        }

        public string Apply(EditorContext context, ApplyRequest request, bool matchIndentation)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!(context?.Handle is EditorHandle handle) || handle.View.IsClosed)
                return "The editor that was active when the assistant opened has been closed.";
            if (string.IsNullOrEmpty(request?.Text))
                return null;

            IWpfTextView view = handle.View;
            ITextBuffer buffer = view.TextBuffer;
            ITextSnapshot snapshot = buffer.CurrentSnapshot;
            string newLine = context.NewLine;
            string text = TextTools.NormalizeNewLines(request.Text, newLine);

            SnapshotSpan target;
            string insertText;

            if (request.Mode == ApplyMode.InsertBelow)
            {
                SnapshotSpan current = handle.Span.GetSpan(snapshot);
                ITextSnapshotLine endLine = LastLineOf(current);
                string body = matchIndentation && request.IsCode ? TextTools.Reindent(text.TrimEnd('\r', '\n'), context.BaseIndent, false) : text.TrimEnd('\r', '\n');

                if (endLine.LineBreakLength > 0)
                {
                    target = new SnapshotSpan(endLine.EndIncludingLineBreak, 0);
                    insertText = body + newLine;
                }
                else
                {
                    target = new SnapshotSpan(endLine.End, 0);
                    insertText = newLine + body;
                }
            }
            else if (handle.BoxSpans != null)
            {
                // Column (box) selections are only replaced line-for-line; anything else would
                // overwrite text outside the box.
                return TryApplyToBox(handle, buffer, text)
                    ? null
                    : $"The result has a different number of lines than the {handle.BoxSpans.Length}-line column selection.";
            }
            else
            {
                target = handle.Span.GetSpan(snapshot);
                string body = text;
                if (matchIndentation && request.IsCode)
                    body = TextTools.Reindent(body.TrimEnd('\r', '\n'), context.BaseIndent, context.StartsMidLine);

                // Keep the line structure around the selection intact.
                bool endsWithBreak = body.EndsWith("\n", StringComparison.Ordinal) || body.EndsWith("\r", StringComparison.Ordinal);
                if (handle.SelectionEndsWithLineBreak && !endsWithBreak)
                    body += newLine;
                else if (!handle.SelectionEndsWithLineBreak && endsWithBreak && !handle.ReplaceWholeLine)
                    body = body.TrimEnd('\r', '\n');
                else if (handle.ReplaceWholeLine)
                    body = body.TrimEnd('\r', '\n');

                insertText = body;
            }

            return ApplyEdit(view, target, insertText);
        }

        private string ApplyEdit(IWpfTextView view, SnapshotSpan target, string insertText)
        {
            ITextBuffer buffer = view.TextBuffer;
            if (!buffer.CheckEditAccess())
                return "The document cannot be edited right now.";

            ITextUndoTransaction transaction = null;
            try
            {
                if (_undoRegistry != null && _undoRegistry.TryGetHistory(buffer, out var history))
                    transaction = history.CreateTransaction("Apply AI Assistant result");

                ITextSnapshot after;
                using (var edit = buffer.CreateEdit())
                {
                    if (!edit.Replace(target, insertText))
                    {
                        edit.Cancel();
                        return "The document is read-only.";
                    }
                    after = edit.Apply();
                    if (edit.Canceled)
                        return "The edit was cancelled by Visual Studio (is the file read-only or locked?).";
                }

                transaction?.Complete();

                var start = new SnapshotPoint(target.Snapshot, target.Start.Position).TranslateTo(after, PointTrackingMode.Negative);
                var inserted = new SnapshotSpan(after, start.Position, Math.Min(insertText.Length, after.Length - start.Position));
                view.Selection.Select(inserted, isReversed: false);
                view.Caret.MoveTo(inserted.End);
                view.ViewScroller.EnsureSpanVisible(inserted, EnsureSpanVisibleOptions.MinimumScroll);
                return null;
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        private bool TryApplyToBox(EditorHandle handle, ITextBuffer buffer, string text)
        {
            var lines = TextTools.SplitLines(text.TrimEnd('\r', '\n'));
            if (lines.Count != handle.BoxSpans.Length || !buffer.CheckEditAccess())
                return false;

            ITextSnapshot snapshot = buffer.CurrentSnapshot;
            ITextUndoTransaction transaction = null;
            try
            {
                if (_undoRegistry != null && _undoRegistry.TryGetHistory(buffer, out var history))
                    transaction = history.CreateTransaction("Apply AI Assistant result");

                using (var edit = buffer.CreateEdit())
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (!edit.Replace(handle.BoxSpans[i].GetSpan(snapshot), lines[i]))
                        {
                            edit.Cancel();
                            return false;
                        }
                    }
                    edit.Apply();
                    if (edit.Canceled)
                        return false;
                }
                transaction?.Complete();
                return true;
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        private IWpfTextView GetActiveView()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_textManager == null || _adapters == null)
                return null;
            if (_textManager.GetActiveView(0, null, out IVsTextView vsTextView) != 0 || vsTextView == null)
                return null;
            return _adapters.GetWpfTextView(vsTextView);
        }

        private static string GetFilePath(ITextBuffer buffer)
        {
            if (buffer != null && buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
                return document.FilePath;
            return null;
        }

        private static ITextSnapshotLine LastLineOf(SnapshotSpan span)
        {
            // A selection ending at column 0 (full-line selection) really ends on the previous line.
            var endLine = span.End.GetContainingLine();
            if (span.Length > 0 && span.End == endLine.Start && endLine.LineNumber > 0)
                endLine = span.Snapshot.GetLineFromLineNumber(endLine.LineNumber - 1);
            return endLine;
        }

        private static string GetNewLine(IWpfTextView view, ITextSnapshot snapshot, SnapshotPoint near)
        {
            string lineBreak = near.GetContainingLine().GetLineBreakText();
            if (!string.IsNullOrEmpty(lineBreak))
                return lineBreak;
            if (snapshot.LineCount > 1)
                return snapshot.GetLineFromLineNumber(0).GetLineBreakText();
            string option = view.Options.GetOptionValue(DefaultOptions.NewLineCharacterOptionId);
            return string.IsNullOrEmpty(option) ? Environment.NewLine : option;
        }
    }
}
