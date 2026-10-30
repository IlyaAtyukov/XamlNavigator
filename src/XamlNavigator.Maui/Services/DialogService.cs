using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using XamlNavigator.Abstractions;
using XamlNavigator.Core;
using XamlNavigator.Localization;
using XamlNavigator.Platform;

namespace XamlNavigator.Services
{
    /// <summary>
    /// Модель диалога + ContentView по имени (XxxDialogViewModel → XxxDialogView), показ модально.
    /// «Назад» на Android закрывает диалог с результатом null.
    /// </summary>
    public class DialogService : IDialogService
    {
        private readonly INavigationServiceProvider _provider;

        public DialogService() : this(null) { }

        public DialogService(INavigationServiceProvider provider)
        {
            _provider = provider;
            MauiPlatform.Install();
        }

        public Task<bool?> ShowAsync(BaseDialogViewModel viewModel) => OnUi(() => Show(viewModel));

        public Task<bool?> ShowAsync<TViewModel>() where TViewModel : BaseDialogViewModel =>
            OnUi(() => Show((BaseDialogViewModel)Create(typeof(TViewModel))));

        private static async Task<bool?> Show(BaseDialogViewModel vm)
        {
            // Активное окно (в многооконном приложении диалог должен открыться там, где работает пользователь).
            var windows = Application.Current?.Windows;
            var navigation = (windows?.FirstOrDefault(w => w.IsActivated) ?? windows?.FirstOrDefault())?.Page?.Navigation;
            if (navigation == null) throw new InvalidOperationException(Tr.T("No active window."));

            var host = new DialogHostPage { Content = ResolveView(vm), BindingContext = vm };
            var tcs = new TaskCompletionSource<bool?>();

            Action<bool?> onClose = null;
            onClose = result =>
            {
                vm.CloseRequested -= onClose;
                if (tcs.TrySetResult(result)) _ = navigation.PopModalAsync();
            };
            vm.CloseRequested += onClose;
            host.Dismissed += () =>
            {
                vm.CloseRequested -= onClose;
                tcs.TrySetResult(null);
            };

            await navigation.PushModalAsync(host);
            await vm.OnAppearingAsync();
            var dialogResult = await tcs.Task;
            await vm.OnDisappearingAsync();
            return dialogResult;
        }

        private static View ResolveView(object vm)
        {
            var viewType = ViewLocator.ViewTypeFor(vm.GetType());
            if (viewType == null) throw new InvalidOperationException(Tr.F("No view found for {0}.", vm.GetType().Name));
            var view = Activator.CreateInstance(viewType) as View;
            if (view == null) throw new InvalidOperationException(Tr.F("{0} must be a View (ContentView).", viewType.Name));
            return view;
        }

        private object Create(Type type) =>
            _provider != null ? ViewBinder.Create(type, _provider) : Activator.CreateInstance(type);

        private static async Task<bool?> OnUi(Func<Task<bool?>> show)
        {
            bool? result = null;
            await NavigatorPlatform.Dispatcher.InvokeAsync(async () => result = await show());
            return result;
        }
    }

    /// <summary>Модальная страница-хост диалога с затемнённым фоном.</summary>
    public class DialogHostPage : ContentPage
    {
        internal event Action Dismissed;

        public DialogHostPage() => BackgroundColor = Color.FromRgba(0, 0, 0, 0.45);

        protected override bool OnBackButtonPressed()
        {
            Dismissed?.Invoke();
            _ = Navigation.PopModalAsync();
            return true;
        }
    }
}
