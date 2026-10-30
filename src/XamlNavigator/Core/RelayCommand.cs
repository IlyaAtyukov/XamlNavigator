using System;
using System.Threading.Tasks;
using System.Windows.Input;
using XamlNavigator.Platform;

namespace XamlNavigator.Core
{
    /// <summary>
    /// Синхронная или асинхронная команда; повторный запуск, пока асинхронная выполняется, запрещён.
    /// Доступность перепроверяется через платформу: в WPF — CommandManager, как обычно; в MAUI —
    /// <see cref="RaiseCanExecuteChanged"/> или <see cref="InvalidateRequerySuggested"/>.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Func<object, Task<bool>> _asyncExecute;
        private readonly Action<object> _syncExecute;
        private readonly Func<object, bool> _canExecute;
        private bool _isExecuting;

        /// <summary>Асинхронная команда завершилась; true — успешно (исключение даёт false).</summary>
        public event EventHandler<bool> ExecutionCompleted;

        /// <summary>
        /// Исключение в асинхронной команде любой модели. Команда его не пробрасывает (async void уронил бы
        /// приложение), поэтому без подписки на это событие ошибка пропадает молча.
        /// </summary>
        public static event EventHandler<Exception> ExecutionFailed;

        /// <summary>
        /// Сообщить в <see cref="ExecutionFailed"/> об исключении, которое некуда пробросить (обработчик async void
        /// и т. п.). Используется платформами навигатора.
        /// </summary>
        public static void ReportFailure(object sender, Exception exception) => ExecutionFailed?.Invoke(sender, exception);

        // Платформа подключается до первой команды: иначе кнопки, привязанные раньше NavigatorBuilder.Build(),
        // жили бы без CommandManager (WPF) до самой установки.
        static RelayCommand() => NavigatorPlatform.TryEnsureInstalled();

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _syncExecute = execute;
            _canExecute = canExecute;
        }

        public RelayCommand(Func<object, Task<bool>> execute, Func<object, bool> canExecute = null)
        {
            _asyncExecute = execute;
            _canExecute = canExecute;
        }

        public RelayCommand(Func<object, Task> execute, Func<object, bool> canExecute = null)
        {
            _asyncExecute = async o => { await execute(o); return true; };
            _canExecute = canExecute;
        }

        /// <summary>
        /// Обёртка над чужой командой: выполняет её, потом <paramref name="after"/>. Синхронная или асинхронная —
        /// как <paramref name="inner"/>; подписчики обёртки слышат и RaiseCanExecuteChanged внутренней команды.
        /// </summary>
        internal RelayCommand(RelayCommand inner, Action after)
        {
            _linked = inner;
            _canExecute = inner.CanExecute;
            if (inner.IsAsync)
                // Как inner.Execute: без проверки CanExecute (первый пункт меню открывается всегда, как раньше)
                // и с ExecutionCompleted самой внутренней команды.
                _asyncExecute = async o =>
                {
                    if (inner._isExecuting) return false;
                    var ok = await inner.RunAsync(o);
                    inner.ExecutionCompleted?.Invoke(inner, ok);
                    after();
                    return ok;
                };
            else
                _syncExecute = o =>
                {
                    inner.Execute(o);
                    after();
                };
        }

        private readonly RelayCommand _linked;

        public event EventHandler CanExecuteChanged
        {
            add
            {
                NavigatorPlatform.CommandRequery.Subscribe(this, value);
                if (_linked != null) NavigatorPlatform.CommandRequery.Subscribe(_linked, value);
            }
            remove
            {
                NavigatorPlatform.CommandRequery.Unsubscribe(this, value);
                if (_linked != null) NavigatorPlatform.CommandRequery.Unsubscribe(_linked, value);
            }
        }

        public bool IsAsync => _asyncExecute != null;

        public bool IsExecuting => _isExecuting;

        public bool CanExecute(object parameter) => !_isExecuting && (_canExecute == null || _canExecute(parameter));

        /// <summary>Перепроверить доступность этой команды.</summary>
        public void RaiseCanExecuteChanged() => NavigatorPlatform.CommandRequery.Invalidate(this);

        /// <summary>Перепроверить доступность всех команд (CommandManager.InvalidateRequerySuggested в WPF).</summary>
        public static void InvalidateRequerySuggested() => NavigatorPlatform.CommandRequery.InvalidateAll();

        public async void Execute(object parameter)
        {
            if (_isExecuting) return;
            if (_asyncExecute == null)
            {
                _syncExecute?.Invoke(parameter);
                return;
            }

            var result = await RunAsync(parameter);
            var dispatcher = NavigatorPlatform.Dispatcher;
            if (dispatcher.CheckAccess()) ExecutionCompleted?.Invoke(this, result);
            else dispatcher.Invoke(() => ExecutionCompleted?.Invoke(this, result));
        }

        /// <summary>Выполнить и дождаться — для тестов и вызова из кода. Недоступная команда не выполняется.</summary>
        public async Task<bool> ExecuteAsync(object parameter = null)
        {
            if (!CanExecute(parameter)) return false;
            if (_asyncExecute == null)
            {
                _syncExecute?.Invoke(parameter);
                return true;
            }
            return await RunAsync(parameter);
        }

        private async Task<bool> RunAsync(object parameter)
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();
            try { return await _asyncExecute(parameter); }
            catch (Exception ex)
            {
                ExecutionFailed?.Invoke(this, ex);
                return false;
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }
    }
}
