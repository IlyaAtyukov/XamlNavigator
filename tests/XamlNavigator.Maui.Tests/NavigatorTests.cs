using XamlNavigator.Abstractions;
using XamlNavigator.Core;
using XamlNavigator.Maui.Tests.Nav.ViewModels;
using XamlNavigator.Services;
using Xunit;

namespace XamlNavigator.Maui.Tests.Nav.ViewModels
{
    using XamlNavigator.Attributes;

    public class HomeViewModel() : BasePageViewModel(typeof(FirstViewModel));

    [ChildOf(typeof(HomeViewModel), Order = 1, DisplayName = "Второй")]
    public class SecondViewModel : BaseContentViewModel;

    [ChildOf(typeof(HomeViewModel), Order = 0, DisplayName = "Первый", IconKey = "1")]
    public class FirstViewModel : BaseContentViewModel
    {
        public int Appeared { get; private set; }
        public override async Task OnAppearingAsync()
        {
            await base.OnAppearingAsync();
            Appeared++;
        }
    }

    [ChildOf(typeof(HomeViewModel), ShowInMenu = false, DisplayName = "Подробно")]
    public class DetailViewModel(Dependency dependency) : BaseContentViewModel
    {
        public Dependency Dependency { get; } = dependency;
        public string Received { get; private set; }
        public override async Task OnAppearingAsync()
        {
            await base.OnAppearingAsync();
            if (HasParam<string>()) Received = TakeParam<string>();
        }
    }

    /// <summary>Оболочка без стартового экрана: показывает первый пункт меню.</summary>
    public class PlainViewModel() : BasePageViewModel;

    [ChildOf(typeof(PlainViewModel), Order = 5, DisplayName = "Последний")]
    public class LateViewModel : BaseContentViewModel;

    [ChildOf(typeof(PlainViewModel), Order = 1, DisplayName = "Ранний")]
    public class EarlyViewModel : BaseContentViewModel;

    public class Dependency;
}

namespace XamlNavigator.Maui.Tests.Nav.Views
{
    public class HomeView : XamlNavigator.Controls.NavigatorPage;
    public class FirstView : ContentView;
    public class SecondView : ContentView;
    public class DetailView : ContentView;
    public class PlainView : ContentPage;
    public class LateView : ContentView;
    public class EarlyView : ContentView;
}

namespace XamlNavigator.Maui.Tests
{
    /// <summary>Соглашение об именах, [ChildOf], история «назад», параметры (перенесено из BotService.Tests).</summary>
    public class NavigatorTests
    {
        private sealed class Provider : INavigationServiceProvider
        {
            public object GetService(Type serviceType) => serviceType == typeof(Dependency) ? new Dependency() : null;
            public T GetService<T>() where T : class => GetService(typeof(T)) as T;
        }

        private static INavigationOrchestrator Build() =>
            NavigatorBuilder.Create(new Provider()).ScanAssembly(typeof(NavigatorTests).Assembly).Build();

        private static (INavigationOrchestrator Nav, HomeViewModel Home) Start()
        {
            var nav = Build();
            var page = nav.CreateRootPage<HomeViewModel>();
            return (nav, Assert.IsType<HomeViewModel>(page.BindingContext));
        }

        [Fact]
        public void Children_become_menu_in_order_hidden_ones_excluded()
        {
            var (_, home) = Start();
            Assert.Equal(["Первый", "Второй"], home.NavigationClusters.SelectMany(c => c.Items).Select(i => i.DisplayName));
            Assert.Equal(3, home.NavigationItems.Count);
            Assert.IsType<FirstViewModel>(home.CurrentView!.ViewModel);
            Assert.IsType<Nav.Views.FirstView>(home.CurrentView.View);
            Assert.True(home.NavigationItems.Single(i => i.DisplayName == "Первый").IsSelected);
        }

        [Fact]
        public async Task Navigate_with_parameter_and_back()
        {
            var (nav, home) = Start();
            var history = 0;
            nav.HistoryChanged += (_, _) => history++;

            await nav.NavigateToAsync<DetailViewModel, string>("26.10");
            var detail = Assert.IsType<DetailViewModel>(home.CurrentView!.ViewModel);
            Assert.Equal("26.10", detail.Received);
            Assert.NotNull(detail.Dependency);   // зависимость из контейнера
            Assert.True(nav.CanNavigateBack);
            Assert.Equal("Подробно", home.DisplayName);

            await nav.NavigateBackAsync();
            Assert.IsType<FirstViewModel>(home.CurrentView!.ViewModel);
            Assert.False(nav.CanNavigateBack);
            Assert.Equal(2, history);
        }

        [Fact]
        public async Task Menu_navigation_clears_history()
        {
            var (nav, home) = Start();
            await nav.NavigateToAsync<DetailViewModel>();
            Assert.True(nav.CanNavigateBack);

            var second = home.NavigationClusters.SelectMany(c => c.Items).Single(i => i.DisplayName == "Второй");
            await second.Command.ExecuteAsync();

            Assert.IsType<SecondViewModel>(home.CurrentView!.ViewModel);
            Assert.False(nav.CanNavigateBack);
            Assert.True(second.IsSelected);
        }

        [Fact]
        public async Task Same_screen_instance_is_reused_and_reappears()
        {
            var (nav, home) = Start();
            var first = Assert.IsType<FirstViewModel>(home.CurrentView!.ViewModel);
            await nav.NavigateToAsync<SecondViewModel>();
            await nav.NavigateBackAsync();
            Assert.Same(first, home.CurrentView!.ViewModel);
            Assert.Equal(1, first.Appeared);   // стартовый экран появился при возврате
        }

        [Fact]
        public void Shell_without_start_view_shows_first_menu_item()
        {
            var nav = Build();
            var page = nav.CreateRootPage<PlainViewModel>();
            var plain = Assert.IsType<PlainViewModel>(page.BindingContext);

            Assert.IsType<EarlyViewModel>(plain.CurrentView!.ViewModel);
            Assert.IsType<Nav.Views.EarlyView>(plain.CurrentView.View);
            Assert.Equal("Ранний", plain.DisplayName);
            Assert.Same(plain, nav.CurrentShell);
        }
    }
}
