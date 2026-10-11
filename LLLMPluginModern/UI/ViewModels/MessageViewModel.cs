using System;
using System.Windows.Input;

namespace LLLMPluginModern.UI.ViewModels
{
    public enum MessageKind
    {
        Info,
        Warning,
        Error,
        About
    }

    public class MessageViewModel : ViewModelBase
    {
        public MessageViewModel(string title, string message, MessageKind kind = MessageKind.Info)
        {
            Title = title ?? "";
            Message = message ?? "";
            Kind = kind;
            OkCommand = new RelayCommand(_ => CloseAction?.Invoke(true));
        }

        public Action<bool> CloseAction { get; set; }

        public string Title { get; }
        public string Message { get; }
        public MessageKind Kind { get; }
        public bool IsAbout => Kind == MessageKind.About;

        /// <summary>Secondary line (version for the About box).</summary>
        public string Detail { get; set; }

        public string LinkText { get; set; }
        public string LinkUrl { get; set; }
        public bool HasLink => !string.IsNullOrEmpty(LinkUrl);

        public ICommand OkCommand { get; }
    }
}
