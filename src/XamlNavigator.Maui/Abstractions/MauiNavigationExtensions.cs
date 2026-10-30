using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using XamlNavigator.Core;
using XamlNavigator.Localization;

namespace XamlNavigator.Abstractions
{
    /// <summary>Имена методов оркестратора, привычные для MAUI.</summary>
    public static class MauiNavigationExtensions
    {
        /// <summary>Создать страницу-оболочку и поставить её в окно приложения вместо текущей.</summary>
        public static Task<bool> OpenPageAsync<TViewModel>(this INavigationOrchestrator orchestrator) where TViewModel : BaseShellViewModel =>
            orchestrator.OpenAsync<TViewModel>();

        /// <summary>
        /// Стартовая страница для <c>Application.CreateWindow</c>: окна ещё нет, поэтому страница
        /// создаётся и становится активной, а показывает её сама платформа.
        /// </summary>
        public static Page CreateRootPage<TViewModel>(this INavigationOrchestrator orchestrator) where TViewModel : BaseShellViewModel
        {
            var page = orchestrator.CreateRootView<TViewModel>() as Page;
            if (page == null) throw new InvalidOperationException(Tr.F("The view for {0} is not a Page.", typeof(TViewModel).Name));
            return page;
        }
    }
}
