using System;
using System.Collections.ObjectModel;
using System.Globalization;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Core.Text;

namespace LLLMPluginModern.UI.ViewModels
{
    public enum ChatItemKind
    {
        User,
        Assistant,
        Tool
    }

    /// <summary>One entry of the conversation: a user prompt, a model answer or a text-tool result.</summary>
    public sealed class ChatItemViewModel : ViewModelBase
    {
        private string _text = "";
        private string _reasoning = "";
        private bool _isStreaming;
        private bool _isReasoningExpanded;
        private string _errorMessage;
        private string _notice;
        private string _stats;
        private bool _isLast;

        private ChatItemViewModel(ChatItemKind kind, string title)
        {
            Kind = kind;
            Title = title ?? "";
        }

        public ChatItemKind Kind { get; }
        public bool IsUser => Kind == ChatItemKind.User;
        public bool IsAssistant => Kind == ChatItemKind.Assistant;
        public bool IsTool => Kind == ChatItemKind.Tool;

        /// <summary>Model name for answers, tool name for tool results.</summary>
        public string Title { get; }

        /// <summary>What the user typed (user items only).</summary>
        public string DisplayText { get; private set; } = "";

        /// <summary>"Program.cs · 12 lines" when code was attached to the prompt.</summary>
        public string AttachmentLabel { get; private set; }

        public bool HasAttachment => !string.IsNullOrEmpty(AttachmentLabel);

        /// <summary>Exact user content sent to the model; used to retry a failed answer.</summary>
        public string SourceUserContent { get; private set; }

        /// <summary>Code that was attached to the prompt of this answer (null when none).</summary>
        public string AttachedCode { get; private set; }

        public ObservableCollection<ResponseBlockViewModel> Blocks { get; } = new ObservableCollection<ResponseBlockViewModel>();

        public string Text
        {
            get => _text;
            private set => SetProperty(ref _text, value ?? "");
        }

        public string Reasoning
        {
            get => _reasoning;
            private set
            {
                if (SetProperty(ref _reasoning, value ?? ""))
                    OnPropertyChanged(nameof(HasReasoning));
            }
        }

        /// <summary>Mirrors the "Show the model's reasoning" setting.</summary>
        public bool ShowReasoning { get; set; } = true;

        public bool HasReasoning => ShowReasoning && _reasoning.Trim().Length > 0;

        public bool IsReasoningExpanded
        {
            get => _isReasoningExpanded;
            set => SetProperty(ref _isReasoningExpanded, value);
        }

        public bool IsStreaming
        {
            get => _isStreaming;
            private set
            {
                if (SetProperty(ref _isStreaming, value))
                    OnPropertyChanged(nameof(IsWaiting));
            }
        }

        /// <summary>Streaming but no answer text yet ("thinking").</summary>
        public bool IsWaiting => _isStreaming && Blocks.Count == 0;

        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (SetProperty(ref _errorMessage, value))
                    OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(_errorMessage);

        public string Notice
        {
            get => _notice;
            private set
            {
                if (SetProperty(ref _notice, value))
                    OnPropertyChanged(nameof(HasNotice));
            }
        }

        public bool HasNotice => !string.IsNullOrEmpty(_notice);

        public string Stats
        {
            get => _stats;
            private set => SetProperty(ref _stats, value);
        }

        public bool IsLast
        {
            get => _isLast;
            set
            {
                if (SetProperty(ref _isLast, value))
                    OnPropertyChanged(nameof(CanRegenerate));
            }
        }

        public bool Succeeded { get; private set; }

        public bool CanRegenerate => IsAssistant && _isLast && !_isStreaming;

        /// <summary>True when the item carries something that can be applied or copied.</summary>
        public bool HasResult => (IsTool || (IsAssistant && !IsStreaming)) && _text.Trim().Length > 0;

        public static ChatItemViewModel ForUser(string displayText, string attachmentLabel)
        {
            return new ChatItemViewModel(ChatItemKind.User, "You")
            {
                DisplayText = displayText ?? "",
                AttachmentLabel = attachmentLabel
            };
        }

        public static ChatItemViewModel ForAssistant(string modelName, string sourceUserContent, string attachedCode)
        {
            var item = new ChatItemViewModel(ChatItemKind.Assistant, modelName) { SourceUserContent = sourceUserContent, AttachedCode = attachedCode };
            item.IsStreaming = true;
            return item;
        }

        public static ChatItemViewModel ForTool(string toolName, string result, string languageId)
        {
            var item = new ChatItemViewModel(ChatItemKind.Tool, toolName) { Succeeded = true };
            item.Text = result ?? "";
            item.Blocks.Add(new ResponseBlockViewModel(new MarkdownBlock(MarkdownBlockKind.Code, (result ?? "").TrimEnd('\r', '\n'), languageId)));
            item.Stats = string.Format(CultureInfo.CurrentCulture, "{0:N0} lines", TextTools.CountLines(result));
            return item;
        }

        public void UpdateStreaming(string text, string reasoning)
        {
            if (!_isStreaming)
                return;
            Text = text;
            Reasoning = reasoning;
            SyncBlocks(text);
        }

        public void Complete(LlmResponse response)
        {
            IsStreaming = false;
            Succeeded = response.IsSuccess;

            Text = response.Text;
            Reasoning = response.Reasoning;
            SyncBlocks(response.Text);

            if (response.IsCancelled)
                Notice = "Stopped.";
            else
                Notice = response.Notice;

            if (!response.IsSuccess && !response.IsCancelled)
                ErrorMessage = response.ErrorMessage ?? "The request failed.";

            Stats = FormatStats(response);
            OnPropertyChanged(nameof(HasResult));
            OnPropertyChanged(nameof(CanRegenerate));
            OnPropertyChanged(nameof(IsWaiting));
        }

        private static string FormatStats(LlmResponse response)
        {
            if (!response.IsSuccess)
                return null;
            string seconds = response.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.CurrentCulture) + " s";
            if (response.OutputTokens.HasValue)
                return string.Format(CultureInfo.CurrentCulture, "{0:N0} tokens · {1}", response.OutputTokens.Value, seconds);
            return seconds;
        }

        private void SyncBlocks(string markdown)
        {
            var parsed = Markdown.ParseBlocks(markdown);
            for (int i = 0; i < parsed.Count; i++)
            {
                if (i < Blocks.Count && Blocks[i].IsCode == (parsed[i].Kind == MarkdownBlockKind.Code))
                    Blocks[i].Update(parsed[i]);
                else if (i < Blocks.Count)
                    Blocks[i] = new ResponseBlockViewModel(parsed[i]);
                else
                    Blocks.Add(new ResponseBlockViewModel(parsed[i]));
            }
            while (Blocks.Count > parsed.Count)
                Blocks.RemoveAt(Blocks.Count - 1);

            OnPropertyChanged(nameof(IsWaiting));
        }
    }

    public sealed class ResponseBlockViewModel : ViewModelBase
    {
        private string _content;
        private string _language;
        private bool _isComplete;

        public ResponseBlockViewModel(MarkdownBlock block)
        {
            IsCode = block.Kind == MarkdownBlockKind.Code;
            Update(block);
        }

        public bool IsCode { get; }

        public string Content
        {
            get => _content;
            private set => SetProperty(ref _content, value);
        }

        public string Language
        {
            get => _language;
            private set
            {
                if (SetProperty(ref _language, value))
                    OnPropertyChanged(nameof(LanguageLabel));
            }
        }

        public string LanguageLabel => LanguageMap.DisplayNameForFence(_language);

        public bool IsComplete
        {
            get => _isComplete;
            private set => SetProperty(ref _isComplete, value);
        }

        public void Update(MarkdownBlock block)
        {
            Content = block.Content;
            Language = block.Language;
            IsComplete = block.IsComplete;
        }
    }
}
