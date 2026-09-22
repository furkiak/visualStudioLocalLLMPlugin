using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LLLMPluginModern.UI.ViewModels
{
    public sealed class AsyncCommand : ICommand
    {
        private readonly Func<Task> execute;
        private readonly Func<bool> canExecute;
        private readonly Action onError;
        private bool running;
        public AsyncCommand(Func<Task> execute, Func<bool> canExecute, Action onError)
        { this.execute = execute; this.canExecute = canExecute; this.onError = onError; }
        public bool CanExecute(object parameter) => !running && canExecute();
        public event EventHandler CanExecuteChanged
        { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
        public void Execute(object parameter) { _ = ExecuteAsync(); }
        public async Task ExecuteAsync()
        {
            if (!CanExecute(null)) return;
            running = true;
            CommandManager.InvalidateRequerySuggested();
            try { await execute(); }
            catch (Exception) { onError(); }
            finally { running = false; CommandManager.InvalidateRequerySuggested(); }
        }
    }
}
