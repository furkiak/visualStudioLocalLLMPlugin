using System;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Infrastructure.Factories;
using LLLMPluginModern.Infrastructure.Services;
using LLLMPluginModern.UI.Services;
using LLLMPluginModern.UI.ViewModels;

namespace LLLMPluginModern
{
    /// <summary>Composition root: wires the services together once per Visual Studio session.</summary>
    public sealed class AppServices
    {
        public AppServices(ISettingsService settings, ILlmApiServiceFactory llmFactory, IEditorService editor)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            LlmFactory = llmFactory ?? throw new ArgumentNullException(nameof(llmFactory));
            Editor = editor ?? throw new ArgumentNullException(nameof(editor));
            Clipboard = new ClipboardService();
            Dialogs = new DialogService(settings, llmFactory);
        }

        public ISettingsService Settings { get; }
        public ILlmApiServiceFactory LlmFactory { get; }
        public IEditorService Editor { get; }
        public IDialogService Dialogs { get; }
        public IClipboardService Clipboard { get; }

        public static AppServices CreateDefault(IEditorService editor)
        {
            return new AppServices(new SettingsService(), new LlmApiServiceFactory(), editor);
        }

        public MainViewModel CreateMainViewModel(EditorContext context)
        {
            return new MainViewModel(LlmFactory, Settings, Dialogs, Clipboard, context ?? EditorContext.Empty);
        }
    }
}
