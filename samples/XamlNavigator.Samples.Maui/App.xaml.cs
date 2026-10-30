using XamlNavigator.Abstractions;
using XamlNavigator.Samples.ViewModels;

namespace XamlNavigator.Samples
{
    public partial class App : Application
    {
        private readonly INavigationOrchestrator _navigator;

        public App(INavigationOrchestrator navigator)
        {
            InitializeComponent();
            // Палитра примера светлая: в тёмной теме системы стандартные поля ввода стали бы белыми на белом.
            UserAppTheme = AppTheme.Light;
            _navigator = navigator;
        }

        protected override Window CreateWindow(IActivationState activationState) =>
            new Window(_navigator.CreateRootPage<MainViewModel>())
            {
                Title = "XamlNavigator — Sample",
                Width = 1100,
                Height = 760
            };
    }
}
