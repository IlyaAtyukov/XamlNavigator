using System;
using System.Linq;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using XamlNavigator.Abstractions;
using XamlNavigator.Controls;
using XamlNavigator.Core;
using XamlNavigator.Localization;

namespace XamlNavigator.Platform
{
    /// <summary>Оболочка — Page (лучше NavigatorPage), экран — ContentView, содержимое диалога — любой View.</summary>
    public class MauiViewAdapter : IViewAdapter
    {
        public bool IsShellView(Type viewType) => typeof(Page).IsAssignableFrom(viewType);

        public bool IsContentView(Type viewType) => typeof(ContentView).IsAssignableFrom(viewType);

        public bool IsDialogView(Type viewType) => typeof(View).IsAssignableFrom(viewType);

        public object CreateView(Type viewType) => Activator.CreateInstance(viewType);

        public void Bind(object view, object viewModel)
        {
            var bindable = view as BindableObject;
            if (bindable != null) bindable.BindingContext = viewModel;
        }

        public void AttachShell(object view, BaseShellViewModel viewModel, INavigationOrchestrator orchestrator)
        {
            var page = view as Page;
            if (page == null) throw new InvalidOperationException(Tr.F("{0} is not a Page.", view?.GetType().Name));

            // Модальный диалог поверх страницы шлёт ей Disappearing/Appearing, хотя экран никуда не уходил:
            // такие пары пропускаем, иначе форма новой записи стирала бы введённое.
            var state = new ShellState { ViewModel = viewModel, Orchestrator = orchestrator };
            _states.Remove(page);
            _states.Add(page, state);

            // Обработчики async void: исключение из них уронило бы приложение — отдаём его в NavigationFailed.
            page.Appearing += async (sender, e) =>
            {
                if (state.HiddenByModal)
                {
                    state.HiddenByModal = false;
                    return;
                }
                try { await viewModel.OnShellAppearingAsync(); }
                catch (Exception ex) { Report(orchestrator, ex); }
            };
            page.Disappearing += async (sender, e) =>
            {
                if (page.Navigation.ModalStack.Count > 0)
                {
                    state.HiddenByModal = true;
                    return;
                }
                await DisappearAsync(state);
            };

            var navigatorPage = page as NavigatorPage;
            if (navigatorPage != null)
            {
                navigatorPage.CanGoBack = () => orchestrator.CanNavigateBack;
                navigatorPage.GoBack = orchestrator.NavigateBackAsync;
            }
        }

        private sealed class ShellState
        {
            public BaseShellViewModel ViewModel;
            public INavigationOrchestrator Orchestrator;
            public bool HiddenByModal;
        }

        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Page, ShellState> _states =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Page, ShellState>();

        private static async System.Threading.Tasks.Task DisappearAsync(ShellState state)
        {
            try { await state.ViewModel.OnShellDisappearingAsync(); }
            catch (Exception ex) { Report(state.Orchestrator, ex); }
        }

        private static void Report(INavigationOrchestrator orchestrator, Exception exception)
        {
            var navigator = orchestrator as Services.NavigationOrchestrator;
            if (navigator != null) navigator.ReportFailure(exception);
        }

        /// <summary>Окно, где показана прежняя оболочка; если её нет — первое окно приложения.</summary>
        private static Window WindowFor(object previousView) =>
            (previousView as Page)?.Window ?? Application.Current?.Windows.FirstOrDefault();

        public void PresentShell(object view, object previousView)
        {
            var window = WindowFor(previousView);
            if (window == null)
                throw new InvalidOperationException(Tr.T("No application window: use CreateRootPage in Application.CreateWindow at startup."));
            window.Page = (Page)view;

            // Прежняя оболочка была скрыта диалогом: второго Disappearing MAUI ей не пришлёт — уводим её сами.
            ShellState previous;
            var previousPage = previousView as Page;
            if (previousPage != null && _states.TryGetValue(previousPage, out previous) && previous.HiddenByModal)
            {
                previous.HiddenByModal = false;
                _ = DisappearAsync(previous);
            }
        }

        public void RollbackShell(object view, object previousView)
        {
            var window = (view as Page)?.Window ?? WindowFor(previousView);
            var previous = previousView as Page;
            if (window != null && previous != null && !ReferenceEquals(window.Page, previous)) window.Page = previous;
        }

        public object CreateEmptyContentView(EmptyContentViewModel viewModel)
        {
            var heading = new Label
            {
                Text = viewModel.DisplayName,
                FontSize = 22,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.WordWrap,
                Margin = new Thickness(0, 0, 0, 14)
            };

            var body = new Label
            {
                Text = viewModel.Message,
                FontSize = 14,
                LineHeight = 1.4,
                HorizontalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.WordWrap,
                MaximumWidthRequest = 520
            };

            var card = new Border
            {
                Padding = new Thickness(28, 24),
                StrokeThickness = 1,
                Stroke = Colors.Gray,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Content = new VerticalStackLayout { Children = { heading, body } }
            };

            return new ContentView { Content = card, Padding = new Thickness(20) };
        }
    }
}
