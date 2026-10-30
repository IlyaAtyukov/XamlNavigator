using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XamlNavigator.Abstractions;
using XamlNavigator.Core;
using XamlNavigator.DynamicPages;
using XamlNavigator.Localization;
using XamlNavigator.Models;
using XamlNavigator.Platform;

namespace XamlNavigator.Services
{
    /// <summary>
    /// Одна активная оболочка и переходы между её экранами с историей «назад».
    /// Переход по пункту меню переключает экран без истории; с <see cref="ClearHistoryOnMenuNavigation"/>
    /// он ещё и очищает её.
    /// </summary>
    public class NavigationOrchestrator : INavigationOrchestrator
    {
        private readonly IStaticViewFactory _staticFactory;
        private readonly IDynamicViewFactory _dynamicFactory;
        private readonly IAccessPolicy _accessPolicy;
        private readonly Stack<ViewPair<object, BaseContentViewModel>> _backStack = new Stack<ViewPair<object, BaseContentViewModel>>();
        private ViewPair<object, BaseShellViewModel> _active;

        public NavigationOrchestrator(IStaticViewFactory staticFactory, IDynamicViewFactory dynamicFactory, IAccessPolicy accessPolicy = null)
        {
            _staticFactory = staticFactory;
            _dynamicFactory = dynamicFactory;
            _accessPolicy = accessPolicy;
        }

        public event EventHandler<NavigationErrorEventArgs> NavigationFailed;
        public event EventHandler HistoryChanged;

        public bool CanNavigateBack => _backStack.Count > 0;

        /// <summary>Переход по пункту меню очищает историю «назад».</summary>
        public bool ClearHistoryOnMenuNavigation { get; set; }

        public BaseShellViewModel CurrentShell => _active?.ViewModel;

        /// <summary>Представление активной оболочки (Window, Page).</summary>
        public object CurrentShellView => _active?.View;

        /// <summary>Политика активной оболочки — та же, по которой построено её меню; иначе — из NavigatorBuilder.</summary>
        private IAccessPolicy Policy => CurrentShell?.AccessPolicy ?? _accessPolicy;

        private bool Denied(Type viewModelType)
        {
            var policy = Policy;
            if (policy == null) return false;
            if (policy.CanAccess(viewModelType)) return false;
            NavigationFailed?.Invoke(this, new NavigationErrorEventArgs(Tr.F("You don't have permission to navigate to {0}.", viewModelType.Name)));
            return true;
        }

        private bool DeniedDynamic(long pageId)
        {
            var policy = Policy;
            if (policy == null) return false;
            if (policy.CanAccessLevel(_dynamicFactory.GetRequiredAccessLevel(pageId))) return false;
            NavigationFailed?.Invoke(this, new NavigationErrorEventArgs(Tr.T("You don't have permission to open this page.")));
            return true;
        }

        public Task<bool> OpenAsync<TViewModel>() where TViewModel : BaseShellViewModel => OpenAsync(typeof(TViewModel));

        public async Task<bool> OpenAsync(Type viewModelType)
        {
            var result = false;
            await NavigatorPlatform.Dispatcher.InvokeAsync(() =>
            {
                var previous = _active;
                var previousHistory = _backStack.ToArray();
                ViewPair<object, BaseShellViewModel> shell = null;
                var activated = false;
                try
                {
                    shell = BuildShell(viewModelType);
                    // Сначала активировать: первый пункт меню может сам перейти на динамическую страницу,
                    // и переход должен попасть в новую оболочку, а не в прежнюю.
                    Activate(shell);
                    activated = true;
                    shell.ViewModel.EnsureContent();
                    NavigatorPlatform.ViewAdapter.PresentShell(shell.View, previous?.View);
                    result = true;
                }
                catch (Exception ex)
                {
                    if (activated)
                    {
                        Deactivate(shell);
                        _active = previous;
                        if (previous != null) Subscribe(previous.ViewModel);
                        RestoreHistory(previousHistory);
                    }
                    if (shell != null) TryRollback(shell.View, previous?.View);
                    Fail(ex);
                }
            });
            return result;
        }

        public object CreateRootView<TViewModel>() where TViewModel : BaseShellViewModel
        {
            var shell = BuildShell(typeof(TViewModel));
            Activate(shell);
            shell.ViewModel.EnsureContent();
            return shell.View;
        }

        public Task NavigateToAsync<TViewModel>() where TViewModel : BaseContentViewModel =>
            NavigateWithHistoryAsync(typeof(TViewModel), parent => parent.SetCurrentViewAsync<TViewModel>());

        public Task NavigateToAsync<TViewModel, TParameter>(TParameter parameter) where TViewModel : BaseContentViewModel =>
            NavigateWithHistoryAsync(typeof(TViewModel), parent => parent.SetCurrentViewAsync<TViewModel, TParameter>(parameter));

        private async Task NavigateWithHistoryAsync(Type viewModelType, Func<BaseShellViewModel, Task> transition)
        {
            var parent = CurrentShell;
            if (parent == null) return;
            if (Denied(viewModelType)) return;

            // Переход на уже открытый экран истории не добавляет: «назад» вернуло бы на него же.
            var pushed = parent.IsCurrent(viewModelType) ? null : PushCurrentView(parent);
            try { await transition(parent); }
            catch (Exception ex)
            {
                Unpush(parent, pushed);
                Fail(ex);
            }
        }

        public Task NavigateToDynamicAsync(long pageId) => NavigateToDynamicAsync(pageId, null);

        public async Task NavigateToDynamicAsync(long pageId, object recordToEdit)
        {
            var parent = CurrentShell;
            if (parent == null) return;
            if (DeniedDynamic(pageId)) return;

            ViewPair<object, BaseContentViewModel> pushed = null;
            try
            {
                if (!_dynamicFactory.IsRegistered(pageId))
                    throw new InvalidOperationException(Tr.F("Dynamic page {0} is not registered. Call IDynamicViewFactory.Register() before navigating.", pageId));

                // Повторный выбор уже открытой страницы: та же страница появляется заново (список перечитывается,
                // форма открывается пустой), без новой модели и без записи в историю.
                var current = parent.CurrentView;
                var currentPage = current?.ViewModel as DynamicPageViewModel;
                if (recordToEdit == null && currentPage != null && currentPage.PageId == pageId)
                {
                    currentPage.RequestNew();
                    await parent.SetCurrentViewAsync(current);
                    return;
                }

                var view = _dynamicFactory.CreateView(pageId);
                var dynamicPage = view.ViewModel as DynamicPageViewModel;
                if (dynamicPage != null)
                {
                    if (recordToEdit != null) dynamicPage.RequestEdit(recordToEdit);
                    else dynamicPage.RequestNew();
                }

                if (!ReferenceEquals(view, parent.CurrentView)) pushed = PushCurrentView(parent);
                await parent.SetCurrentViewAsync(view);
            }
            catch (Exception ex)
            {
                Unpush(parent, pushed);
                Fail(ex);
            }
        }

        public async Task NavigateBackAsync()
        {
            var parent = CurrentShell;
            if (_backStack.Count == 0 || parent == null) return;

            var previous = _backStack.Pop();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
            try { await parent.SetCurrentViewAsync(previous); }
            catch (Exception ex) { Fail(ex); }
        }

        /// <summary>
        /// Сообщить об ошибке навигации, случившейся вне оркестратора — например, в обработчике Loaded/Appearing
        /// платформы. Приходит в <see cref="NavigationFailed"/>.
        /// </summary>
        public void ReportFailure(Exception exception) => Fail(exception);

        private ViewPair<object, BaseShellViewModel> BuildShell(Type viewModelType) => _staticFactory.CreateShellView(viewModelType, this);

        private void Activate(ViewPair<object, BaseShellViewModel> shell)
        {
            if (_active != null) Deactivate(_active);
            _active = shell;
            Subscribe(shell.ViewModel);
            ClearHistory();
        }

        private void Subscribe(BaseShellViewModel shell)
        {
            shell.MenuNavigated += OnMenuNavigated;
            shell.NavigationFailed += OnShellNavigationFailed;
        }

        private void Deactivate(ViewPair<object, BaseShellViewModel> shell)
        {
            shell.ViewModel.MenuNavigated -= OnMenuNavigated;
            shell.ViewModel.NavigationFailed -= OnShellNavigationFailed;
        }

        private void OnMenuNavigated()
        {
            if (ClearHistoryOnMenuNavigation) ClearHistory();
        }

        private void OnShellNavigationFailed(object sender, NavigationErrorEventArgs e) => NavigationFailed?.Invoke(this, e);

        private void ClearHistory()
        {
            if (_backStack.Count == 0) return;
            _backStack.Clear();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RestoreHistory(ViewPair<object, BaseContentViewModel>[] history)
        {
            _backStack.Clear();
            // ToArray стека отдаёт верхний элемент первым — возвращаем в обратном порядке.
            foreach (var entry in Enumerable.Reverse(history)) _backStack.Push(entry);
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        private ViewPair<object, BaseContentViewModel> PushCurrentView(BaseShellViewModel parent)
        {
            if (parent.CurrentView == null || parent.IsShowingEmptyContent) return null;
            _backStack.Push(parent.CurrentView);
            HistoryChanged?.Invoke(this, EventArgs.Empty);
            return parent.CurrentView;
        }

        /// <summary>
        /// Убрать запись, добавленную перед неудачным переходом: если экран так и не сменился, а запись всё ещё сверху.
        /// </summary>
        private void Unpush(BaseShellViewModel parent, ViewPair<object, BaseContentViewModel> pushed)
        {
            if (pushed == null || !ReferenceEquals(parent.CurrentView, pushed)) return;
            if (_backStack.Count == 0 || !ReferenceEquals(_backStack.Peek(), pushed)) return;
            _backStack.Pop();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TryRollback(object view, object previousView)
        {
            try { NavigatorPlatform.ViewAdapter.RollbackShell(view, previousView); }
            catch (Exception) { /* откат — по возможности, основная ошибка уже есть */ }
        }

        private void Fail(Exception ex) => NavigationFailed?.Invoke(this, new NavigationErrorEventArgs(ex.Message, ex));
    }
}
