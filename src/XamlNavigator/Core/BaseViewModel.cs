using System;
using System.Threading.Tasks;

namespace XamlNavigator.Core
{
    /// <summary>
    /// INotifyPropertyChanged и жизненный цикл экрана. <see cref="InitializeAsync"/> выполняется один раз
    /// при первом появлении; если упал — повторится при следующем.
    /// </summary>
    public abstract class BaseViewModel : ObservableObject
    {
        private readonly object _initLock = new object();
        private Task _initializationTask;
        private bool _initialized;
        private string _displayName;

        /// <summary>Ошибка в <see cref="InitializeAsync"/> любой модели — например, чтобы записать её в лог.</summary>
        public static event EventHandler<Exception> InitializationFailed;

        public virtual string DisplayName
        {
            get { return _displayName; }
            set
            {
                _displayName = value;
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public bool IsInitialized
        {
            get { lock (_initLock) return _initialized; }
        }

        public virtual async Task OnAppearingAsync()
        {
            Task taskToWait;
            lock (_initLock)
            {
                if (_initializationTask != null) taskToWait = _initializationTask;
                else
                {
                    if (_initialized) return;
                    _initializationTask = InitializeAsyncSafe();
                    taskToWait = _initializationTask;
                }
            }

            try { await taskToWait; }
            finally
            {
                lock (_initLock) if (_initializationTask == taskToWait) _initializationTask = null;
            }
        }

        private async Task InitializeAsyncSafe()
        {
            try
            {
                await InitializeAsync();
                lock (_initLock) _initialized = true;
            }
            catch (Exception ex)
            {
                lock (_initLock) _initializationTask = null;
                OnInitializationFailed(ex);
                InitializationFailed?.Invoke(this, ex);
            }
        }

        protected virtual Task InitializeAsync() => Task.CompletedTask;

        /// <summary>Ошибка инициализации этой модели; например, показать её на экране.</summary>
        protected virtual void OnInitializationFailed(Exception exception) { }

        public virtual Task OnDisappearingAsync() => Task.CompletedTask;
    }
}
