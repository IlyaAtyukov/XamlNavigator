using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using XamlNavigator.Abstractions;
using XamlNavigator.DynamicPages;
using XamlNavigator.Localization;
using XamlNavigator.Samples.Data;
using XamlNavigator.Samples.ViewModels;
using XamlNavigator.Services;

namespace XamlNavigator.Samples
{
    public partial class App : Application
    {
        private INavigationOrchestrator _navigator;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // The sample's own texts are English, so keep the navigator's texts English too. Remove this line to let
            // the navigator follow the system language (Russian is built in; add others with AddTranslations).
            NavigatorLocalization.Language = "en";

            var services = new ServiceCollection()
                .AddSingleton<IDynamicPageRepository, DemoRepository>()
                .AddSingleton<IDynamicViewFactory, DynamicViewFactory>()
                .AddSingleton<IDialogService, DialogService>()
                .AddSingleton(_ => _navigator)
                .BuildServiceProvider();

            _navigator = NavigatorBuilder
                .Create(new ServiceProviderAdapter(services))
                .ScanAssembly(typeof(MainViewModel).Assembly)
                .UseViewsFrom(typeof(App).Assembly)
                .UseDynamicViewFactory(services.GetRequiredService<IDynamicViewFactory>())
                .OnNavigationFailed((navigator, error) => MessageBox.Show(error.FullMessage, "Navigation", MessageBoxButton.OK, MessageBoxImage.Warning))
                .Build();

            _ = _navigator.OpenWindowAsync<MainViewModel>();
        }
    }
}
