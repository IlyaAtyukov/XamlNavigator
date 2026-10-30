using System;
using System.Threading.Tasks;
using XamlNavigator.Platform;

namespace XamlNavigator.Core
{
    /// <summary>
    /// Модель страницы-оболочки MAUI (ContentPage, лучше NavigatorPage), внутри которой меняются экраны
    /// с [ChildOf] на неё.
    /// </summary>
    public abstract class BasePageViewModel : BaseShellViewModel
    {
        static BasePageViewModel() => MauiPlatform.Install();

        /// <param name="startViewModelType">Модель экрана, который показать первым; null — первый пункт меню.</param>
        protected BasePageViewModel(Type startViewModelType = null) : base(startViewModelType) { }
    }

    /// <summary>Выполнить в потоке интерфейса; без запущенного приложения (тесты) — на месте.</summary>
    public static class UiThread
    {
        public static Task InvokeAsync(Action action) => NavigatorPlatform.Dispatcher.InvokeAsync(action);

        public static Task InvokeAsync(Func<Task> action) => NavigatorPlatform.Dispatcher.InvokeAsync(action);
    }
}
