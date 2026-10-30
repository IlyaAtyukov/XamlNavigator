using System;
using System.Threading.Tasks;
using XamlNavigator.Core;
using XamlNavigator.Models;

namespace XamlNavigator.Abstractions
{
    /// <summary>
    /// Одна активная оболочка (окно WPF, страница MAUI) и переходы между её экранами с историей «назад».
    /// Платформенные имена — OpenWindowAsync (WPF), OpenPageAsync и CreateRootPage (MAUI) — это
    /// методы-расширения из пакетов платформ.
    /// </summary>
    public interface INavigationOrchestrator
    {
        /// <summary>Создать оболочку и показать её вместо текущей. false — не получилось (см. NavigationFailed).</summary>
        Task<bool> OpenAsync<TViewModel>() where TViewModel : BaseShellViewModel;

        /// <summary>
        /// Создать оболочку и сделать её активной, но не показывать: для старта приложения, когда
        /// представление нужно вернуть платформе (MAUI Application.CreateWindow).
        /// </summary>
        object CreateRootView<TViewModel>() where TViewModel : BaseShellViewModel;

        Task NavigateToAsync<TViewModel>() where TViewModel : BaseContentViewModel;
        Task NavigateToAsync<TViewModel, TParameter>(TParameter parameter) where TViewModel : BaseContentViewModel;
        Task NavigateToDynamicAsync(long pageId);
        /// <summary>Открыть динамическую страницу; с <paramref name="recordToEdit"/> — форму этой записи (null — новая запись).</summary>
        Task NavigateToDynamicAsync(long pageId, object recordToEdit);
        Task NavigateBackAsync();
        bool CanNavigateBack { get; }

        /// <summary>Модель активной оболочки или null, если ничего ещё не открыто.</summary>
        BaseShellViewModel CurrentShell { get; }

        event EventHandler<NavigationErrorEventArgs> NavigationFailed;

        /// <summary>Изменилась история «назад» — например, чтобы показать или спрятать кнопку «Назад».</summary>
        event EventHandler HistoryChanged;
    }
}
