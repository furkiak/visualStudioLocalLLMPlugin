using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LLLMPluginModern.UI.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            if (CanExecute(parameter))
                _execute(parameter);
        }

        /// <summary>Asks WPF to re-evaluate CanExecute for every command (e.g. after a busy flag changes).</summary>
        public static void Refresh()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// Command for async work. Exceptions are routed to <c>onError</c> instead of escaping as an
    /// unobserved async-void exception (which would take Visual Studio down with it).
    /// </summary>
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<object, Task> _execute;
        private readonly Predicate<object> _canExecute;
        private readonly Action<Exception> _onError;
        private bool _isRunning;

        public AsyncRelayCommand(Func<object, Task> execute, Predicate<object> canExecute = null, Action<Exception> onError = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _onError = onError;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool IsRunning => _isRunning;

        public bool CanExecute(object parameter)
        {
            return !_isRunning && (_canExecute == null || _canExecute(parameter));
        }

        public void Execute(object parameter)
        {
            _ = ExecuteAsync(parameter);
        }

        public async Task ExecuteAsync(object parameter)
        {
            if (!CanExecute(parameter))
                return;

            _isRunning = true;
            RelayCommand.Refresh();
            try
            {
                await _execute(parameter);
            }
            catch (Exception ex)
            {
                _onError?.Invoke(ex);
            }
            finally
            {
                _isRunning = false;
                RelayCommand.Refresh();
            }
        }
    }
}
