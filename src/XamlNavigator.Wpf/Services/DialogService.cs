using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using XamlNavigator.Abstractions;
using XamlNavigator.Core;
using XamlNavigator.Localization;
using XamlNavigator.Platform;

namespace XamlNavigator.Services
{
    /// <summary>
    /// Модальный диалог: модель + UserControl по имени (XxxDialogViewModel → XxxDialogView) в окне
    /// <see cref="DialogHostWindow"/>. Задача завершается, когда окно закрыто, — на потоке интерфейса
    /// она к этому моменту уже выполнена.
    /// </summary>
    public class DialogService : IDialogService
    {
        private readonly INavigationServiceProvider _provider;

        static DialogService() => WpfPlatform.Install();

        public DialogService() { }

        /// <summary>С контейнером: модели для <see cref="ShowAsync{TViewModel}"/> получают зависимости из него.</summary>
        public DialogService(INavigationServiceProvider provider) => _provider = provider;

        public Task<bool?> ShowAsync(BaseDialogViewModel viewModel) => ShowDialog(viewModel);

        /// <summary>Без контейнера модель создаётся конструктором без параметров, как в прежнем WpfNavigator.</summary>
        public Task<bool?> ShowAsync<TViewModel>() where TViewModel : BaseDialogViewModel =>
            ShowDialog(_provider == null
                ? (BaseDialogViewModel)Activator.CreateInstance(typeof(TViewModel))
                : (BaseDialogViewModel)ViewBinder.Create(typeof(TViewModel), _provider));

        private static Task<bool?> ShowDialog(BaseDialogViewModel vm)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) throw new InvalidOperationException(Tr.T("Application.Current is not available."));
            if (!dispatcher.CheckAccess()) return dispatcher.Invoke(() => ShowDialog(vm));
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return Task.FromResult<bool?>(null);

            var tcs = new TaskCompletionSource<bool?>();

            var view = ResolveView(vm);
            var owner = ResolveOwner();
            var host = new DialogHostWindow
            {
                Content = view,
                DataContext = vm,
                Owner = owner
            };

            if (owner == null)
            {
                host.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                host.ShowInTaskbar = true;
                host.Topmost = true;
            }

            Action<bool?> onClose = null;
            onClose = result =>
            {
                vm.CloseRequested -= onClose;
                tcs.TrySetResult(result);
                if (host.IsLoaded) host.Close();
            };
            vm.CloseRequested += onClose;

            // Обработчики async void: исключение из них уронило бы приложение. Ошибка при открытии завершает задачу
            // диалога этим исключением и закрывает окно; ошибка при закрытии — в RelayCommand.ExecutionFailed.
            host.Loaded += async (sender, e) =>
            {
                try { await vm.OnAppearingAsync(); }
                catch (Exception ex)
                {
                    vm.CloseRequested -= onClose;
                    if (tcs.TrySetException(ex) && host.IsLoaded) host.Close();
                }
            };
            host.Closed += async (sender, e) =>
            {
                vm.CloseRequested -= onClose;
                tcs.TrySetResult(host.DialogResult);
                try { await vm.OnDisappearingAsync(); }
                catch (Exception ex) { RelayCommand.ReportFailure(vm, ex); }
            };

            // Диалог без владельца — единственное окно (или WPF сделал его MainWindow): его закрытие не должно
            // завершать приложение ни в OnLastWindowClose, ни в OnMainWindowClose.
            var application = Application.Current;
            var previousMode = application.ShutdownMode;
            var guardShutdown = owner == null && previousMode != ShutdownMode.OnExplicitShutdown;
            if (guardShutdown)
            {
                try { application.ShutdownMode = ShutdownMode.OnExplicitShutdown; }
                catch (InvalidOperationException)
                {
                    // Приложение уже завершается (Shutdown() вызван) — диалог показывать некому.
                    return Task.FromResult<bool?>(null);
                }
            }

            try { host.ShowDialog(); }
            finally
            {
                if (ReferenceEquals(application.MainWindow, host)) application.MainWindow = null;
                if (guardShutdown && !dispatcher.HasShutdownStarted)
                {
                    try { application.ShutdownMode = previousMode; }
                    catch (InvalidOperationException) { /* Shutdown() уже вызван — режим больше не важен */ }
                }
            }

            if (!tcs.Task.IsCompleted) tcs.TrySetResult(host.DialogResult);
            return tcs.Task;
        }

        private static UserControl ResolveView(object vm)
        {
            var viewType = ViewLocator.ViewTypeFor(vm.GetType());
            if (viewType == null) throw new InvalidOperationException(Tr.F("No view found for {0}.", vm.GetType().Name));

            var userControl = Activator.CreateInstance(viewType) as UserControl;
            if (userControl == null) throw new InvalidOperationException(Tr.F("{0} must be a UserControl.", viewType.Name));
            return userControl;
        }

        private static Window ResolveOwner()
        {
            var main = Application.Current?.MainWindow;
            if (main == null || !main.IsLoaded) return null;
            return main;
        }
    }
}
