using System;
using XamlNavigator.Abstractions;
using XamlNavigator.Core;

namespace XamlNavigator.Platform
{
    /// <summary>
    /// Всё, что ядро делает с представлениями, через платформу: в WPF «оболочка» — Window,
    /// экран — UserControl; в MAUI — Page и ContentView.
    /// </summary>
    public interface IViewAdapter
    {
        /// <summary>Тип годится для оболочки (окна или страницы), внутри которой меняются экраны.</summary>
        bool IsShellView(Type viewType);

        /// <summary>Тип годится для экрана-содержимого внутри оболочки.</summary>
        bool IsContentView(Type viewType);

        /// <summary>Тип годится для содержимого диалога.</summary>
        bool IsDialogView(Type viewType);

        object CreateView(Type viewType);

        /// <summary>Связать представление с моделью: DataContext в WPF, BindingContext в MAUI.</summary>
        void Bind(object view, object viewModel);

        /// <summary>
        /// Подписать модель оболочки на жизненный цикл представления (Loaded/Closing, Appearing/Disappearing)
        /// и, где есть, на аппаратную «Назад».
        /// </summary>
        void AttachShell(object view, BaseShellViewModel viewModel, INavigationOrchestrator orchestrator);

        /// <summary>Показать новую оболочку вместо прежней. Вызывается в потоке интерфейса.</summary>
        void PresentShell(object view, object previousView);

        /// <summary>Откатить неудачный показ: вернуть прежнюю оболочку, закрыть недопоказанную.</summary>
        void RollbackShell(object view, object previousView);

        /// <summary>Заглушка «страниц пока нет» для оболочки без экранов.</summary>
        object CreateEmptyContentView(EmptyContentViewModel viewModel);
    }
}
