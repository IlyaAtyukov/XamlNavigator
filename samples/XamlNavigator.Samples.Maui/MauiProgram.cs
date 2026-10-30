using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using XamlNavigator.Abstractions;
using XamlNavigator.Core;
using XamlNavigator.DynamicPages;
using XamlNavigator.Platform;
using XamlNavigator.Samples.Data;
using XamlNavigator.Samples.ViewModels;
using XamlNavigator.Services;

namespace XamlNavigator.Samples
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseXamlNavigator();

            // The sample's own texts are English, so keep the navigator's texts English too. Remove this line to let
            // the navigator follow the device language (Russian is built in; add others with AddTranslations).
            XamlNavigator.Localization.NavigatorLocalization.Language = "en";

            builder.Services.AddSingleton<IDynamicPageRepository, DemoRepository>();
            builder.Services.AddSingleton<IDynamicViewFactory, DynamicViewFactory>();
            builder.Services.AddSingleton<IDialogService>(sp => new DialogService(new ServiceProviderAdapter(sp)));
            builder.Services.AddSingleton(sp => NavigatorBuilder
                .Create(new ServiceProviderAdapter(sp))
                .ScanAssembly(typeof(MainViewModel).Assembly)   // модели — в общей библиотеке
                .UseViewsFrom(typeof(App).Assembly)             // представления — здесь, XamlNavigator.Samples.Views
                .UseDynamicViewFactory(sp.GetRequiredService<IDynamicViewFactory>())
                .OnNavigationFailed((navigator, error) => ShowError(error.FullMessage))
                .Build());

            // Без подписки ошибки в асинхронных командах и в InitializeAsync пропадают молча.
            RelayCommand.ExecutionFailed += (sender, ex) => ShowError(ex.Message);
            BaseViewModel.InitializationFailed += (sender, ex) => ShowError(sender.GetType().Name + ": " + ex.Message);

            return builder.Build();
        }

        private static void ShowError(string message)
        {
            Trace.WriteLine("[XamlNavigator] " + message);
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page != null) MainThread.BeginInvokeOnMainThread(() => _ = page.DisplayAlertAsync("Error", message, "OK"));
        }
    }
}
