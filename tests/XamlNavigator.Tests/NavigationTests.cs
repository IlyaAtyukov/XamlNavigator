using System;
using System.Linq;
using System.Threading.Tasks;
using XamlNavigator.Abstractions;
using XamlNavigator.Attributes;
using XamlNavigator.Core;
using XamlNavigator.Models;
using XamlNavigator.Services;
using XamlNavigator.Tests.Nav.ViewModels;
using Xunit;

namespace XamlNavigator.Tests.Nav.ViewModels
{
    public class HomeViewModel : BaseShellViewModel
    {
        public HomeViewModel(INavigationOrchestrator navigator) : base(typeof(FirstViewModel)) => Navigator = navigator;
        public INavigationOrchestrator Navigator { get; }
    }

    public class LoginViewModel : BaseShellViewModel { }

    public class EmptyShellViewModel : BaseShellViewModel { }

    [ChildOf(typeof(HomeViewModel), Order = 1, DisplayName = "Второй")]
    public class SecondViewModel : BaseContentViewModel
    {
        public int Disappeared { get; private set; }

        public override Task OnDisappearingAsync()
        {
            Disappeared++;
            return base.OnDisappearingAsync();
        }
    }

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

    [ChildOf(typeof(HomeViewModel), Cluster = 2, DisplayName = "Админка")]
    public class AdminViewModel : BaseContentViewModel { }

    [ChildOf(typeof(HomeViewModel), ShowInMenu = false, DisplayName = "Подробно")]
    public class DetailViewModel : BaseContentViewModel
    {
        public DetailViewModel(Dependency dependency, string optional = "по умолчанию")
        {
            Dependency = dependency;
            Optional = optional;
        }

        public Dependency Dependency { get; }
        public string Optional { get; }
        public string Received { get; private set; }

        public override async Task OnAppearingAsync()
        {
            await base.OnAppearingAsync();
            if (HasParam<string>()) Received = TakeParam<string>();
        }

        public Task GoSecond() => NavigateParentToAsync<SecondViewModel>();

        public Task GoFlaky() => NavigateParentToAsync<FlakyViewModel>();

        public Task GoAdmin() => NavigateParentToAsync<AdminViewModel>();
    }

    [ChildOf(typeof(LoginViewModel), DisplayName = "Вход")]
    public class SignInViewModel : BaseContentViewModel
    {
        public int Appeared { get; private set; }

        public override async Task OnAppearingAsync()
        {
            await base.OnAppearingAsync();
            Appeared++;
        }
    }

    /// <summary>Экран, который не создаётся с первого раза: проверка, что сбой не запоминается навсегда.</summary>
    [ChildOf(typeof(HomeViewModel), ShowInMenu = false, DisplayName = "Капризный")]
    public class FlakyViewModel : BaseContentViewModel
    {
        public static int Attempts;

        public FlakyViewModel()
        {
            if (++Attempts == 1) throw new InvalidOperationException("первая попытка не удалась");
        }
    }

    public enum Mode { First, Second }

    public class OptionalsViewModel : BaseContentViewModel
    {
        public OptionalsViewModel(DateTime since = default(DateTime), Mode mode = Mode.Second, int? limit = null)
        {
            Since = since;
            Mode = mode;
            Limit = limit;
        }

        public DateTime Since { get; }
        public Mode Mode { get; }
        public int? Limit { get; }
    }

    [ChildOf(typeof(LoginViewModel), Order = 1, DisplayName = "Регистрация")]
    public class SignUpViewModel : BaseContentViewModel { }

    public class Dependency { }

    public class NotRegisteredViewModel : BaseContentViewModel
    {
        public NotRegisteredViewModel(Uri missing) { }
    }
}

namespace XamlNavigator.Tests.Nav.Views
{
    public class HomeView : FakeShellView { }
    public class LoginView : FakeShellView { }
    public class EmptyShellView : FakeShellView { }
    public class FirstView : FakeContentView { }
    public class SecondView : FakeContentView { }
    public class AdminView : FakeContentView { }
    public class DetailView : FakeContentView { }
    public class SignInView : FakeContentView { }
    public class SignUpView : FakeContentView { }
    public class NotRegisteredView : FakeContentView { }
    public class FlakyView : FakeContentView { }
    public class OptionalsView : FakeContentView { }
}

namespace XamlNavigator.Tests
{
    public class NavigationTests
    {
        public NavigationTests() => TestPlatform.Install();

        private static NavigationOrchestrator Build(LevelPolicy policy = null, bool clearHistory = false)
        {
            var builder = NavigatorBuilder.Create(new Provider().Add(new Dependency()))
                .ScanAssembly(typeof(NavigationTests).Assembly)
                .ClearHistoryOnMenuNavigation(clearHistory);
            if (policy != null) builder.UseAccessPolicy(policy);
            return (NavigationOrchestrator)builder.Build();
        }

        private static HomeViewModel Home(NavigationOrchestrator nav)
        {
            var view = (FakeView)nav.CreateRootView<HomeViewModel>();
            return Assert.IsType<HomeViewModel>(view.BindingContext);
        }

        private static NavigationItem MenuItem(BaseShellViewModel shell, string name) =>
            shell.NavigationClusters.SelectMany(c => c.Items).Single(i => i.DisplayName == name);

        [Fact]
        public void Children_become_menu_in_cluster_and_order_hidden_ones_excluded()
        {
            var home = Home(Build());

            Assert.Equal(new[] { "Первый", "Второй", "Админка" }, home.NavigationClusters.SelectMany(c => c.Items).Select(i => i.DisplayName));
            Assert.Equal(new[] { 0, 2 }, home.NavigationClusters.Select(c => c.Number));
            Assert.Equal(5, home.NavigationItems.Count);
            Assert.IsType<FirstViewModel>(home.CurrentView.ViewModel);
            Assert.IsType<Nav.Views.FirstView>(home.CurrentView.View);
            Assert.Equal("Первый", home.DisplayName);
            Assert.True(MenuItem(home, "Первый").IsSelected);
            Assert.Equal("1", MenuItem(home, "Первый").Icon);
        }

        [Fact]
        public async Task Navigate_with_parameter_and_back()
        {
            var nav = Build();
            var home = Home(nav);
            var history = 0;
            nav.HistoryChanged += (s, e) => history++;

            await nav.NavigateToAsync<DetailViewModel, string>("26.10");
            var detail = Assert.IsType<DetailViewModel>(home.CurrentView.ViewModel);
            Assert.Equal("26.10", detail.Received);
            Assert.NotNull(detail.Dependency);
            Assert.Equal("по умолчанию", detail.Optional);
            Assert.True(nav.CanNavigateBack);
            Assert.Equal("Подробно", home.DisplayName);
            Assert.Same(home, detail.ParentShell);
            Assert.Same(nav, home.Navigator);

            await nav.NavigateBackAsync();
            Assert.IsType<FirstViewModel>(home.CurrentView.ViewModel);
            Assert.False(nav.CanNavigateBack);
            Assert.Equal(2, history);
        }

        [Fact]
        public async Task Same_screen_instance_is_reused_and_reappears()
        {
            var nav = Build();
            var home = Home(nav);
            var first = Assert.IsType<FirstViewModel>(home.CurrentView.ViewModel);

            await nav.NavigateToAsync<SecondViewModel>();
            var second = (SecondViewModel)home.CurrentView.ViewModel;
            await nav.NavigateBackAsync();

            Assert.Same(first, home.CurrentView.ViewModel);
            Assert.Equal(1, first.Appeared);
            Assert.Equal(1, second.Disappeared);
        }

        [Fact]
        public async Task Reselecting_current_screen_does_not_fire_disappearing()
        {
            var nav = Build();
            var home = Home(nav);
            var item = MenuItem(home, "Второй");

            await item.Command.ExecuteAsync();
            await item.Command.ExecuteAsync();

            var second = Assert.IsType<SecondViewModel>(home.CurrentView.ViewModel);
            Assert.Equal(0, second.Disappeared);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task Menu_navigation_history_depends_on_option(bool clearHistory, bool canGoBackAfterMenu)
        {
            var nav = Build(clearHistory: clearHistory);
            var home = Home(nav);
            await nav.NavigateToAsync<DetailViewModel>();
            Assert.True(nav.CanNavigateBack);

            var second = MenuItem(home, "Второй");
            await second.Command.ExecuteAsync();

            Assert.IsType<SecondViewModel>(home.CurrentView.ViewModel);
            Assert.Equal(canGoBackAfterMenu, nav.CanNavigateBack);
            Assert.True(second.IsSelected);
            Assert.False(MenuItem(home, "Первый").IsSelected);
        }

        [Fact]
        public async Task Parent_navigation_changes_screen_without_history()
        {
            var nav = Build();
            var home = Home(nav);
            await nav.NavigateToAsync<DetailViewModel>();
            var detail = (DetailViewModel)home.CurrentView.ViewModel;

            await detail.GoSecond();

            Assert.IsType<SecondViewModel>(home.CurrentView.ViewModel);
            Assert.True(nav.CanNavigateBack);
            await nav.NavigateBackAsync();
            Assert.IsType<FirstViewModel>(home.CurrentView.ViewModel);
        }

        [Fact]
        public async Task Access_policy_hides_menu_and_blocks_navigation()
        {
            var policy = new LevelPolicy();
            policy.Denied.Add(typeof(AdminViewModel));
            var nav = Build(policy);
            var home = Home(nav);
            NavigationErrorEventArgs failure = null;
            nav.NavigationFailed += (s, e) => failure = e;

            Assert.DoesNotContain(home.NavigationClusters.SelectMany(c => c.Items), i => i.DisplayName == "Админка");
            Assert.Single(home.NavigationItems, i => i.DisplayName == "Админка" && !i.Visible);

            await nav.NavigateToAsync<AdminViewModel>();

            Assert.IsType<FirstViewModel>(home.CurrentView.ViewModel);
            Assert.NotNull(failure);
            Assert.Contains("AdminViewModel", failure.Message);
        }

        [Fact]
        public async Task Shell_without_start_view_shows_first_menu_item()
        {
            var nav = Build();
            Assert.True(await nav.OpenAsync<LoginViewModel>());

            var login = Assert.IsType<LoginViewModel>(nav.CurrentShell);
            Assert.IsType<SignInViewModel>(login.CurrentView.ViewModel);
            Assert.True(((FakeShellView)nav.CurrentShellView).Shown);
        }

        [Fact]
        public async Task Shell_without_screens_shows_empty_content()
        {
            var nav = Build();
            await nav.OpenAsync<EmptyShellViewModel>();

            var shell = nav.CurrentShell;
            Assert.True(shell.IsShowingEmptyContent);
            Assert.IsType<FakeEmptyView>(shell.CurrentView.View);
            Assert.Equal(shell.EmptyContentTitle, shell.CurrentView.ViewModel.DisplayName);
        }

        [Fact]
        public async Task Opening_another_shell_closes_previous_and_clears_history()
        {
            var nav = Build();
            await nav.OpenAsync<HomeViewModel>();
            var homeView = (FakeShellView)nav.CurrentShellView;
            await nav.NavigateToAsync<DetailViewModel>();
            Assert.True(nav.CanNavigateBack);

            await nav.OpenAsync<LoginViewModel>();

            Assert.True(homeView.Closed);
            Assert.IsType<LoginViewModel>(nav.CurrentShell);
            Assert.False(nav.CanNavigateBack);
        }

        [Fact]
        public async Task Failed_open_reports_error_and_keeps_current_shell()
        {
            var nav = Build();
            await nav.OpenAsync<HomeViewModel>();
            var errors = 0;
            nav.NavigationFailed += (s, e) => errors++;

            var opened = await nav.OpenAsync<ShellWithoutViewModel>();

            Assert.False(opened);
            Assert.Equal(1, errors);
            Assert.IsType<HomeViewModel>(nav.CurrentShell);
        }

        [Fact]
        public void Missing_dependency_is_named_in_error()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => ViewBinder.Create(typeof(NotRegisteredViewModel), new Provider()));
            Assert.Contains("Uri", ex.Message);
        }

        [Fact]
        public void Dynamic_menu_items_are_added_filtered_by_level_and_removed()
        {
            var policy = new LevelPolicy { Level = 1 };
            var home = Home(Build(policy));
            var executed = 0;

            home.AddDynamicNavigationItem(10, "Заказы", new RelayCommand(_ => executed++), requiredAccessLevel: 1);
            home.AddDynamicNavigationItem(10, "Дубль", new RelayCommand(_ => { }));
            home.AddDynamicNavigationItem(11, "Секрет", new RelayCommand(_ => { }), requiredAccessLevel: 5);

            var orders = MenuItem(home, "Заказы");
            Assert.Equal(1, orders.Cluster);
            Assert.DoesNotContain(home.NavigationClusters.SelectMany(c => c.Items), i => i.DisplayName == "Секрет" || i.DisplayName == "Дубль");
            orders.Command.Execute(null);
            Assert.Equal(1, executed);

            home.RemoveDynamicNavigationItem(10);
            Assert.DoesNotContain(home.NavigationItems, i => i.DynamicPageId == 10);
            Assert.DoesNotContain(home.NavigationClusters, c => c.Number == 1 && c.Items.Count == 0);
        }

        [Fact]
        public async Task First_screen_of_shell_without_start_view_appears_once()
        {
            var nav = Build();
            await nav.OpenAsync<LoginViewModel>();
            var login = (LoginViewModel)nav.CurrentShell;
            var signIn = (SignInViewModel)login.CurrentView.ViewModel;

            await login.OnShellAppearingAsync();   // так делает платформа на Loaded/Appearing

            Assert.Equal(1, signIn.Appeared);
        }

        [Fact]
        public async Task Shell_disappearing_and_appearing_again_reappears_current_screen()
        {
            var nav = Build();
            await nav.OpenAsync<LoginViewModel>();
            var login = (LoginViewModel)nav.CurrentShell;
            var signIn = (SignInViewModel)login.CurrentView.ViewModel;
            await login.OnShellAppearingAsync();

            await login.OnShellDisappearingAsync();
            await login.OnShellAppearingAsync();

            Assert.Equal(2, signIn.Appeared);
        }

        [Fact]
        public void Denied_start_view_is_not_shown()
        {
            var policy = new LevelPolicy();
            policy.Denied.Add(typeof(FirstViewModel));
            var home = Home(Build(policy));

            Assert.IsType<SecondViewModel>(home.CurrentView.ViewModel);
        }

        [Fact]
        public async Task Failed_or_same_screen_navigation_leaves_no_history()
        {
            var nav = Build();
            var home = Home(nav);
            var failures = 0;
            nav.NavigationFailed += (s, e) => failures++;

            await nav.NavigateToAsync<NotRegisteredViewModel>();   // не экран этой оболочки
            Assert.Equal(1, failures);
            Assert.False(nav.CanNavigateBack);

            await nav.NavigateToAsync<FirstViewModel>();           // он и так открыт
            Assert.False(nav.CanNavigateBack);
            Assert.IsType<FirstViewModel>(home.CurrentView.ViewModel);
        }

        [Fact]
        public async Task Menu_navigation_errors_reach_NavigationFailed_and_failed_screen_is_retried()
        {
            var nav = Build();
            var home = Home(nav);
            NavigationErrorEventArgs failure = null;
            nav.NavigationFailed += (s, e) => failure = e;
            var flaky = home.NavigationItems.Single(i => i.ViewModelType == typeof(FlakyViewModel));

            FlakyViewModel.Attempts = 0;
            flaky.Command.Execute(null);
            Assert.NotNull(failure);
            Assert.Contains("первая попытка", failure.FullMessage);

            flaky.Command.Execute(null);
            await Task.Yield();
            Assert.IsType<FlakyViewModel>(home.CurrentView.ViewModel);
            Assert.False(flaky.Command.IsAsync);
        }

        [Fact]
        public void Refresh_access_rebuilds_menu_for_new_rights()
        {
            var policy = new LevelPolicy();
            policy.Denied.Add(typeof(AdminViewModel));
            var home = Home(Build(policy));
            home.AddDynamicNavigationItem(7, "Отчёты", new RelayCommand(_ => { }), requiredAccessLevel: 2);
            Assert.DoesNotContain(home.NavigationClusters.SelectMany(c => c.Items), i => i.DisplayName == "Админка" || i.DisplayName == "Отчёты");

            policy.Denied.Clear();
            policy.Level = 2;
            home.RefreshAccess();

            var menu = home.NavigationClusters.SelectMany(c => c.Items).Select(i => i.DisplayName).ToList();
            Assert.Equal(new[] { "Первый", "Второй", "Отчёты", "Админка" }, menu);
            Assert.DoesNotContain("Подробно", menu);
        }

        [Fact]
        public async Task Failed_open_keeps_previous_shell_and_its_history()
        {
            var nav = Build();
            await nav.OpenAsync<HomeViewModel>();
            await nav.NavigateToAsync<DetailViewModel>();

            Assert.False(await nav.OpenAsync<ShellWithoutViewModel>());

            Assert.IsType<HomeViewModel>(nav.CurrentShell);
            Assert.True(nav.CanNavigateBack);
            await nav.NavigateBackAsync();
            Assert.IsType<FirstViewModel>(nav.CurrentShell.CurrentView.ViewModel);
        }

        [Fact]
        public void Optional_constructor_parameters_get_their_defaults()
        {
            var vm = (OptionalsViewModel)ViewBinder.Create(typeof(OptionalsViewModel), new Provider());

            Assert.Equal(default(DateTime), vm.Since);
            Assert.Equal(Mode.Second, vm.Mode);
            Assert.Null(vm.Limit);
        }

        [Fact]
        public void Dynamic_menu_item_hears_its_original_command()
        {
            var home = Home(Build());
            var original = new RelayCommand(_ => { });
            home.AddDynamicNavigationItem(8, "Склад", original);
            var item = home.NavigationItems.Single(i => i.DynamicPageId == 8);
            var raised = 0;
            item.Command.CanExecuteChanged += (s, e) => raised++;

            original.RaiseCanExecuteChanged();

            Assert.Equal(1, raised);
        }

        [Fact]
        public async Task Reselecting_start_screen_before_shell_appears_does_not_appear_it_twice()
        {
            var nav = Build();
            await nav.OpenAsync<HomeViewModel>();
            var home = (HomeViewModel)nav.CurrentShell;
            var first = (FirstViewModel)home.CurrentView.ViewModel;

            await MenuItem(home, "Первый").Command.ExecuteAsync();
            await home.OnShellAppearingAsync();

            Assert.Equal(1, first.Appeared);
        }

        [Fact]
        public async Task New_policy_on_the_shell_applies_to_menu_and_to_navigation()
        {
            var strict = new LevelPolicy();
            strict.Denied.Add(typeof(AdminViewModel));
            var nav = Build(strict);
            var home = Home(nav);
            var failures = 0;
            nav.NavigationFailed += (s, e) => failures++;

            home.AccessPolicy = new LevelPolicy();
            await nav.NavigateToAsync<AdminViewModel>();

            Assert.Contains(home.NavigationClusters.SelectMany(c => c.Items), i => i.DisplayName == "Админка");
            Assert.IsType<AdminViewModel>(home.CurrentView.ViewModel);
            Assert.Equal(0, failures);
        }

        [Fact]
        public async Task Parent_navigation_errors_and_denials_reach_NavigationFailed()
        {
            var policy = new LevelPolicy();
            policy.Denied.Add(typeof(AdminViewModel));
            var nav = Build(policy);
            var home = Home(nav);
            var errors = new System.Collections.Generic.List<string>();
            nav.NavigationFailed += (s, e) => errors.Add(e.FullMessage);
            await nav.NavigateToAsync<DetailViewModel>();
            var detail = (DetailViewModel)home.CurrentView.ViewModel;

            FlakyViewModel.Attempts = 0;
            await detail.GoFlaky();
            await detail.GoAdmin();

            Assert.Equal(2, errors.Count);
            Assert.Contains("первая попытка", errors[0]);
            Assert.Contains("AdminViewModel", errors[1]);
            Assert.Same(detail, home.CurrentView.ViewModel);
        }

        [Fact]
        public async Task Async_dynamic_menu_item_navigates_even_while_its_command_is_unavailable()
        {
            var executed = 0;
            var after = 0;
            var inner = new RelayCommand(async _ => { await Task.Yield(); executed++; }, _ => false);
            var wrapper = new RelayCommand(inner, () => after++);
            var completed = false;
            inner.ExecutionCompleted += (s, ok) => completed = ok;

            wrapper.Execute(null);
            for (var i = 0; i < 50 && after == 0; i++) await Task.Delay(10);

            Assert.Equal(1, executed);
            Assert.Equal(1, after);
            Assert.True(completed);
        }

        public class ShellWithoutViewModel : BaseShellViewModel { }
    }
}
