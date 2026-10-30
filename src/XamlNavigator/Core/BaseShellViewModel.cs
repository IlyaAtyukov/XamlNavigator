using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using XamlNavigator.Abstractions;
using XamlNavigator.Attributes;
using XamlNavigator.DynamicPages;
using XamlNavigator.Localization;
using XamlNavigator.Models;
using XamlNavigator.Platform;

namespace XamlNavigator.Core
{
    /// <summary>
    /// Модель оболочки (окно WPF, страница MAUI), внутри которой меняются экраны с
    /// <see cref="ChildOfAttribute"/> на неё. Экраны создаются лениво, по одному на оболочку.
    /// В приложении удобнее наследовать платформенные имена: BaseWindowViewModel (WPF) или BasePageViewModel (MAUI).
    /// </summary>
    public abstract class BaseShellViewModel : BaseViewModel
    {
        private readonly Dictionary<Type, Lazy<ViewPair<object, BaseContentViewModel>>> _childFactories = new Dictionary<Type, Lazy<ViewPair<object, BaseContentViewModel>>>();
        private readonly HashSet<long> _dynamicPageIds = new HashSet<long>();
        private readonly Dictionary<NavigationItem, ItemAccess> _itemAccess = new Dictionary<NavigationItem, ItemAccess>();
        private ViewPair<object, BaseContentViewModel> _currentView;
        private ViewPair<object, BaseContentViewModel> _emptyContent;
        private IAccessPolicy _accessPolicy;
        private bool _currentAppeared;
        private readonly Type _startViewModelType;

        /// <summary>Как пункт меню проверяется по правам: видимость по замыслу и проверка политикой.</summary>
        private sealed class ItemAccess
        {
            public bool ShownByDesign;
            public Func<IAccessPolicy, bool> Allowed;
        }

        /// <param name="startViewModelType">Модель экрана, который показать первым (typeof(HomeViewModel)); null — первый пункт меню.</param>
        protected BaseShellViewModel(Type startViewModelType = null) => _startViewModelType = startViewModelType;

        /// <summary>Права доступа. Смена политики сразу пересчитывает меню (см. <see cref="RefreshAccess"/>).</summary>
        public IAccessPolicy AccessPolicy
        {
            get { return _accessPolicy; }
            set
            {
                _accessPolicy = value;
                if (NavigationItems.Count > 0) RefreshAccess();
            }
        }

        public virtual string HeaderCaption => "";

        public ObservableCollection<NavigationItem> NavigationItems { get; } = new ObservableCollection<NavigationItem>();
        public ObservableCollection<NavigationCluster> NavigationClusters { get; } = new ObservableCollection<NavigationCluster>();

        /// <summary>
        /// Переход по пункту меню. Оркестратор может очищать на него историю «назад» (так по умолчанию в MAUI:
        /// аппаратная «Назад» после перехода по меню не должна возвращать на давно покинутый экран).
        /// </summary>
        public event Action MenuNavigated;

        /// <summary>
        /// Переход по пункту меню не удался. Команда меню не бросает исключение в интерфейс — ошибка приходит сюда,
        /// а оркестратор передаёт её в свой NavigationFailed.
        /// </summary>
        public event EventHandler<NavigationErrorEventArgs> NavigationFailed;

        public ViewPair<object, BaseContentViewModel> CurrentView
        {
            get { return _currentView; }
            set
            {
                if (Equals(value, _currentView)) return;
                _currentView = value;
                OnPropertyChanged(nameof(CurrentView));
                OnPropertyChanged(nameof(IsShowingEmptyContent));
                UpdateSelection();
            }
        }

        public string EmptyContentTitle { get; set; } = Tr.T("No pages yet");

        public string EmptyContentMessage { get; set; } = Tr.T("Pages you have access to will appear here.");

        public bool IsShowingEmptyContent => _emptyContent != null && ReferenceEquals(CurrentView, _emptyContent);

        /// <summary>
        /// Если ничего не показано — показать стартовый экран (если он доступен по правам), иначе первый доступный
        /// пункт меню, иначе заглушку <see cref="EmptyContentTitle"/>.
        /// </summary>
        public void EnsureContent()
        {
            NavigatorPlatform.Dispatcher.Invoke(() =>
            {
                if (CurrentView != null && !IsShowingEmptyContent) return;

                Lazy<ViewPair<object, BaseContentViewModel>> factory;
                if (_startViewModelType != null && IsAllowed(_startViewModelType) && _childFactories.TryGetValue(_startViewModelType, out factory))
                {
                    // Оболочка уже на экране (стартовый экран стал доступен после смены прав) — обычный переход
                    // с OnDisappearing заглушки и OnAppearing экрана; до показа — экран появится вместе с оболочкой.
                    if (_currentAppeared) Observe(() => TransitionTo(factory.Value));
                    else Show(factory.Value);
                    return;
                }

                var first = NavigationClusters.SelectMany(c => c.Items).FirstOrDefault(i => i.Visible && i.Command != null);
                if (first != null)
                {
                    CurrentView = null;
                    first.Command.Execute(null);
                    return;
                }

                var views = NavigatorPlatform.ViewAdapter;
                if (views == null) return;
                var emptyViewModel = new EmptyContentViewModel(EmptyContentTitle, EmptyContentMessage);
                var emptyView = views.CreateEmptyContentView(emptyViewModel);
                views.Bind(emptyView, emptyViewModel);
                _emptyContent = new ViewPair<object, BaseContentViewModel> { View = emptyView, ViewModel = emptyViewModel };
                Show(_emptyContent);
            });
        }

        private void Show(ViewPair<object, BaseContentViewModel> content)
        {
            CurrentView = content;
            DisplayName = content.ViewModel.DisplayName;
        }

        public override async Task OnAppearingAsync()
        {
            await base.OnAppearingAsync();
            EnsureContent();
        }

        /// <summary>
        /// Оболочка появилась на экране (Loaded в WPF, Appearing в MAUI): её OnAppearingAsync и — один раз — текущего
        /// экрана. Экран, на который оболочка только что перешла сама, второй раз не появляется. Вызывает платформа.
        /// </summary>
        public async Task OnShellAppearingAsync()
        {
            await OnAppearingAsync();
            var current = CurrentView?.ViewModel;
            if (current == null || _currentAppeared) return;
            _currentAppeared = true;
            await current.OnAppearingAsync();
        }

        /// <summary>Оболочка ушла с экрана (Closed в WPF, Disappearing в MAUI): текущий экран, затем она сама. Вызывает платформа.</summary>
        public async Task OnShellDisappearingAsync()
        {
            var current = CurrentView?.ViewModel;
            if (current != null && _currentAppeared)
            {
                _currentAppeared = false;
                await current.OnDisappearingAsync();
            }
            await OnDisappearingAsync();
        }

        /// <summary>
        /// Показать экран этой оболочки: у прежнего — OnDisappearingAsync, у нового — OnAppearingAsync. Историю
        /// «назад» не пишет (для перехода с историей — INavigationOrchestrator.NavigateToAsync).
        /// </summary>
        public Task SetCurrentViewAsync<T>() where T : BaseContentViewModel => SetCurrentViewAsync(typeof(T));

        /// <summary>Показать экран и передать ему параметр (экран забирает его через TakeParam).</summary>
        public async Task SetCurrentViewAsync<TViewModel, TParameter>(TParameter parameter) where TViewModel : BaseContentViewModel
        {
            var factory = Factory(typeof(TViewModel));
            if (parameter != null) factory.Value.ViewModel.SetParam(parameter);
            await TransitionTo(factory.Value);
        }

        public async Task SetCurrentViewAsync(Type viewModelType) => await TransitionTo(Factory(viewModelType).Value);

        /// <summary>Показать готовую пару «представление + модель» (динамическая страница, экран из истории).</summary>
        public Task SetCurrentViewAsync(ViewPair<object, BaseContentViewModel> view) => TransitionTo(view);

        /// <summary>Прежнее имя <see cref="SetCurrentViewAsync{T}()"/>.</summary>
        [Obsolete("Use SetCurrentViewAsync.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Task SetCurrentView<T>() where T : BaseContentViewModel => SetCurrentViewAsync<T>();

        /// <summary>Прежнее имя <see cref="SetCurrentViewAsync{TViewModel, TParameter}(TParameter)"/>.</summary>
        [Obsolete("Use SetCurrentViewAsync.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Task SetCurrentView<TViewModel, TParameter>(TParameter parameter) where TViewModel : BaseContentViewModel =>
            SetCurrentViewAsync<TViewModel, TParameter>(parameter);

        /// <summary>Прежнее имя <see cref="SetCurrentViewAsync(Type)"/>.</summary>
        [Obsolete("Use SetCurrentViewAsync.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Task SetCurrentView(Type viewModelType) => SetCurrentViewAsync(viewModelType);

        /// <summary>Прежнее имя <see cref="SetCurrentViewAsync(ViewPair{object, BaseContentViewModel})"/>.</summary>
        [Obsolete("Use SetCurrentViewAsync.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Task SetArbitraryView(ViewPair<object, BaseContentViewModel> view) => SetCurrentViewAsync(view);

        /// <summary>Есть ли у оболочки экран с такой моделью.</summary>
        public bool HasChild(Type viewModelType) => _childFactories.ContainsKey(viewModelType);

        /// <summary>Уже созданный экран этой модели — тот, на который перейдёт <see cref="SetCurrentViewAsync{T}()"/>.</summary>
        public bool IsCurrent(Type viewModelType)
        {
            Lazy<ViewPair<object, BaseContentViewModel>> factory;
            return CurrentView != null && _childFactories.TryGetValue(viewModelType, out factory)
                   && factory.IsValueCreated && ReferenceEquals(factory.Value, CurrentView);
        }

        private Lazy<ViewPair<object, BaseContentViewModel>> Factory(Type viewModelType)
        {
            Lazy<ViewPair<object, BaseContentViewModel>> factory;
            if (!_childFactories.TryGetValue(viewModelType, out factory))
                throw new InvalidOperationException(Tr.F("No screen for {0} is registered in this shell.", viewModelType.Name));
            return factory;
        }

        private async Task TransitionTo(ViewPair<object, BaseContentViewModel> next)
        {
            if (ReferenceEquals(CurrentView, next))
            {
                // Повторный выбор того же экрана: он остаётся, но новый параметр надо разобрать.
                _currentAppeared = true;
                await next.ViewModel.OnAppearingAsync();
                return;
            }

            // Тот же переход уже идёт (двойной щелчок по синхронной команде меню) — второй раз не уводим и не показываем.
            if (ReferenceEquals(_transitionTarget, next)) return;
            _transitionTarget = next;
            try
            {
                var leaving = CurrentView?.ViewModel;
                if (leaving != null && _currentAppeared)
                {
                    _currentAppeared = false;
                    await leaving.OnDisappearingAsync();
                }
                NavigatorPlatform.Dispatcher.Invoke(() =>
                {
                    CurrentView = next;
                    DisplayName = next.ViewModel.DisplayName;
                    _currentAppeared = true;
                });
            }
            finally
            {
                if (ReferenceEquals(_transitionTarget, next)) _transitionTarget = null;
            }
            await next.ViewModel.OnAppearingAsync();
        }

        private ViewPair<object, BaseContentViewModel> _transitionTarget;

        public void RegisterChild(Func<ViewPair<object, BaseContentViewModel>> factory, Type viewModelType)
        {
            // PublicationOnly не запоминает исключение: экран, который не создался, попробует создаться ещё раз.
            _childFactories[viewModelType] = new Lazy<ViewPair<object, BaseContentViewModel>>(factory, LazyThreadSafetyMode.PublicationOnly);

            var attr = viewModelType.GetCustomAttribute<ChildOfAttribute>();
            var allowed = IsAllowed(viewModelType);
            if (viewModelType == _startViewModelType && allowed)
            {
                CurrentView = _childFactories[viewModelType].Value;
                DisplayName = CurrentView.ViewModel.DisplayName;
                _currentAppeared = false;
            }

            var item = new NavigationItem
            {
                DisplayName = attr?.DisplayName ?? viewModelType.Name,
                Icon = attr?.IconKey,
                // Синхронная, как в прежнем WpfNavigator: кнопки меню не показывают «выполнение» на время перехода.
                Command = new RelayCommand(_ =>
                {
                    MenuNavigated?.Invoke();
                    Observe(() => SetCurrentViewAsync(viewModelType));
                }),
                Cluster = attr?.Cluster ?? 0,
                Order = attr?.Order ?? 0,
                Visible = (attr?.ShowInMenu ?? true) && allowed,
                ViewModelType = viewModelType
            };
            _itemAccess[item] = new ItemAccess { ShownByDesign = attr?.ShowInMenu ?? true, Allowed = policy => policy.CanAccess(viewModelType) };

            NavigationItems.Add(item);
            if (item.Visible) AddToCluster(item);
            UpdateSelection();
        }

        /// <summary>
        /// Пункт меню динамической страницы. <paramref name="command"/> обычно вызывает
        /// <c>NavigateToDynamicAsync(pageId)</c>; после него оболочка сообщает <see cref="MenuNavigated"/>.
        /// </summary>
        public void AddDynamicNavigationItem(long pageId, string displayName, RelayCommand command, int requiredAccessLevel = 0, int cluster = 1, int order = 0, string iconKey = null)
        {
            NavigatorPlatform.Dispatcher.Invoke(() =>
            {
                if (!_dynamicPageIds.Add(pageId)) return;

                var item = new NavigationItem
                {
                    DisplayName = displayName,
                    Icon = iconKey,
                    Command = new RelayCommand(command, () => MenuNavigated?.Invoke()),
                    Cluster = cluster,
                    Order = order,
                    Visible = AccessPolicy == null || AccessPolicy.CanAccessLevel(requiredAccessLevel),
                    DynamicPageId = pageId
                };
                _itemAccess[item] = new ItemAccess { ShownByDesign = true, Allowed = policy => policy.CanAccessLevel(requiredAccessLevel) };

                NavigationItems.Add(item);
                if (item.Visible) AddToCluster(item);
                UpdateSelection();
            });
        }

        public void RemoveDynamicNavigationItem(long pageId)
        {
            NavigatorPlatform.Dispatcher.Invoke(() =>
            {
                if (!_dynamicPageIds.Remove(pageId)) return;
                for (var ci = NavigationClusters.Count - 1; ci >= 0; ci--)
                {
                    var cluster = NavigationClusters[ci];
                    for (var i = cluster.Items.Count - 1; i >= 0; i--)
                    {
                        var tag = cluster.Items[i].DynamicPageId;
                        if (tag.HasValue && tag.Value == pageId) cluster.Items.RemoveAt(i);
                    }

                    if (cluster.Items.Count == 0) NavigationClusters.RemoveAt(ci);
                }
                MarkFirstCluster();

                for (var i = NavigationItems.Count - 1; i >= 0; i--)
                {
                    var tag = NavigationItems[i].DynamicPageId;
                    if (!tag.HasValue || tag.Value != pageId) continue;
                    _itemAccess.Remove(NavigationItems[i]);
                    NavigationItems.RemoveAt(i);
                }
            });
        }

        /// <summary>
        /// Заново проверить права всех пунктов меню и пересобрать группы — например, после входа под другим
        /// пользователем, если политика смотрит на текущую сессию.
        /// </summary>
        public void RefreshAccess()
        {
            NavigatorPlatform.Dispatcher.Invoke(() =>
            {
                NavigationClusters.Clear();
                foreach (var item in NavigationItems)
                {
                    ItemAccess access;
                    if (_itemAccess.TryGetValue(item, out access))
                        item.Visible = access.ShownByDesign && (AccessPolicy == null || access.Allowed(AccessPolicy));
                    if (item.Visible) AddToCluster(item);
                }
                UpdateSelection();
            });
        }

        private bool IsAllowed(Type viewModelType) => AccessPolicy == null || AccessPolicy.CanAccess(viewModelType);

        private async void Observe(Func<Task> navigation) => await ObserveAsync(navigation);

        /// <summary>
        /// Выполнить переход, а ошибку не бросать, а сообщить в <see cref="NavigationFailed"/>. Делегат, а не задача:
        /// экран, который не создаётся, бросает исключение ещё до первого await.
        /// </summary>
        internal async Task ObserveAsync(Func<Task> navigation)
        {
            try { await navigation(); }
            catch (Exception ex) { ReportNavigationFailure(new NavigationErrorEventArgs(ex.Message, ex)); }
        }

        internal void ReportNavigationFailure(NavigationErrorEventArgs error)
        {
            var failed = NavigationFailed;
            if (failed != null) failed(this, error);
            else if (error.Exception != null) RelayCommand.ReportFailure(this, error.Exception);
        }

        private void AddToCluster(NavigationItem item)
        {
            var cluster = NavigationClusters.FirstOrDefault(c => c.Number == item.Cluster);
            if (cluster == null)
            {
                cluster = new NavigationCluster { Number = item.Cluster };
                var insertAt = 0;
                while (insertAt < NavigationClusters.Count && NavigationClusters[insertAt].Number < item.Cluster) insertAt++;
                NavigationClusters.Insert(insertAt, cluster);
                MarkFirstCluster();
            }
            var pos = 0;
            while (pos < cluster.Items.Count && cluster.Items[pos].Order <= item.Order) pos++;
            cluster.Items.Insert(pos, item);
        }

        private void MarkFirstCluster()
        {
            for (var i = 0; i < NavigationClusters.Count; i++) NavigationClusters[i].IsFirst = i == 0;
        }

        private void UpdateSelection()
        {
            var current = CurrentView?.ViewModel;
            var dynamicPage = current as DynamicPageViewModel;
            foreach (var item in NavigationItems)
            {
                var tag = item.DynamicPageId;
                item.IsSelected = item.ViewModelType != null
                    ? current != null && item.ViewModelType == current.GetType()
                    : tag.HasValue && dynamicPage != null && dynamicPage.PageId == tag.Value;
            }
        }
    }
}
