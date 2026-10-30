using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Hosting;
using XamlNavigator.DynamicPages;
using XamlNavigator.Platform;

[assembly: NavigatorPlatform(typeof(MauiPlatform))]

namespace XamlNavigator.Platform
{
    /// <summary>
    /// Подключает .NET MAUI к ядру навигатора. Вызывается сам из NavigatorBuilder.Build(); явно —
    /// <c>builder.UseXamlNavigator()</c> в MauiProgram или <see cref="Install"/>.
    /// </summary>
    public static class MauiPlatform
    {
        private static readonly object Sync = new object();
        private static bool _installed;

        public static void Install()
        {
            lock (Sync)
            {
                if (_installed) return;

                NavigatorPlatform.Dispatcher = new MauiUiDispatcher();
                // По умолчанию в ядре уже ExplicitCommandRequery: не заменяем, чтобы не терять подписки кнопок.
                if (!(NavigatorPlatform.CommandRequery is ExplicitCommandRequery)) NavigatorPlatform.CommandRequery = new ExplicitCommandRequery();
                NavigatorPlatform.RecordsViewFactory = FilteredRecordsViewFactory.Instance;
                NavigatorPlatform.ViewAdapter = new MauiViewAdapter();
                NavigatorPlatform.Printer = null;
                NavigatorPlatform.PhotoService = new MauiPhotoService();
                NavigatorPlatform.DefaultDynamicViewFactory = () => new DynamicViewFactory();
                NavigatorPlatform.ClearHistoryOnMenuNavigationByDefault = true;
                NavigatorPlatform.MarkInstalled("MAUI");

                _installed = true;
            }
        }

        /// <summary>Подключить навигатор в MauiProgram.CreateMauiApp().</summary>
        public static MauiAppBuilder UseXamlNavigator(this MauiAppBuilder builder)
        {
            Install();
            return builder;
        }
    }

    /// <summary>
    /// Поток интерфейса MAUI. Без запущенного приложения (тесты) или если диспетчеризация не нужна —
    /// выполняет на месте: MainThread в net10.0 без платформы бросает исключение.
    /// </summary>
    public sealed class MauiUiDispatcher : IUiDispatcher
    {
        private static IDispatcher Current => Application.Current?.Dispatcher;

        public bool CheckAccess()
        {
            var dispatcher = Current;
            return dispatcher == null || !dispatcher.IsDispatchRequired;
        }

        public void Invoke(Action action)
        {
            var dispatcher = Current;
            if (dispatcher == null || !dispatcher.IsDispatchRequired)
            {
                action();
                return;
            }
            // Вызов идёт из фонового потока: поток интерфейса не заблокирован, ждать безопасно.
            dispatcher.DispatchAsync(action).GetAwaiter().GetResult();
        }

        public Task InvokeAsync(Action action)
        {
            var dispatcher = Current;
            if (dispatcher == null || !dispatcher.IsDispatchRequired)
            {
                action();
                return Task.CompletedTask;
            }
            return dispatcher.DispatchAsync(action);
        }

        public Task InvokeAsync(Func<Task> action)
        {
            var dispatcher = Current;
            if (dispatcher == null || !dispatcher.IsDispatchRequired) return action();
            return dispatcher.DispatchAsync(action);
        }
    }
}
