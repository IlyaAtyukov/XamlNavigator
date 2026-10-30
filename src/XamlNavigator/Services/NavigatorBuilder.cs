using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using XamlNavigator.Abstractions;
using XamlNavigator.Core;
using XamlNavigator.Models;
using XamlNavigator.Platform;

namespace XamlNavigator.Services
{
    /// <summary>
    /// Сканирует сборки и связывает XxxViewModel с XxxView по полному имени
    /// (MyApp.ViewModels.OrdersViewModel → MyApp.Views.OrdersView). Модель без представления пропускается.
    /// </summary>
    public class NavigatorBuilder
    {
        private readonly INavigationServiceProvider _provider;
        private readonly List<Assembly> _assemblies = new List<Assembly>();
        private readonly List<Type> _excludedTypes = new List<Type>();
        private Action<INavigationOrchestrator, NavigationErrorEventArgs> _onFailed;
        private IDynamicViewFactory _dynamicFactory;
        private IAccessPolicy _accessPolicy;
        private bool? _clearHistoryOnMenuNavigation;

        private NavigatorBuilder(INavigationServiceProvider provider) => _provider = provider;

        public static NavigatorBuilder Create(INavigationServiceProvider provider) => new NavigatorBuilder(provider);

        /// <summary>Взять модели из сборки; представления ищутся рядом с ними и в сборках из <see cref="UseViewsFrom"/>.</summary>
        public NavigatorBuilder ScanAssembly(Assembly assembly) { _assemblies.Add(assembly); return this; }

        /// <summary>
        /// Представления лежат в другой сборке, чем модели (модели — в общей библиотеке для WPF и MAUI).
        /// Полное имя представления то же: Shared.ViewModels.HomeViewModel → Shared.Views.HomeView.
        /// </summary>
        public NavigatorBuilder UseViewsFrom(Assembly assembly)
        {
            ViewLocator.AddViewAssembly(assembly);
            return this;
        }
        public NavigatorBuilder ExcludeTypes(params Type[] types) { _excludedTypes.AddRange(types); return this; }
        public NavigatorBuilder OnNavigationFailed(Action<INavigationOrchestrator, NavigationErrorEventArgs> handler) { _onFailed = handler; return this; }

        /// <summary>Прежняя форма (Stronghold): обработчик без причины ошибки.</summary>
        public NavigatorBuilder OnNavigationFailed(Action<INavigationOrchestrator> handler)
        {
            _onFailed = handler == null ? null : (Action<INavigationOrchestrator, NavigationErrorEventArgs>)((navigator, error) => handler(navigator));
            return this;
        }

        public NavigatorBuilder UseDynamicViewFactory(IDynamicViewFactory factory)
        {
            _dynamicFactory = factory;
            return this;
        }

        public NavigatorBuilder UseAccessPolicy(IAccessPolicy policy)
        {
            _accessPolicy = policy;
            return this;
        }

        /// <summary>
        /// Очищать ли историю «назад» при переходе по пункту меню. По умолчанию решает платформа:
        /// в MAUI очищается, в WPF нет.
        /// </summary>
        public NavigatorBuilder ClearHistoryOnMenuNavigation(bool clear = true)
        {
            _clearHistoryOnMenuNavigation = clear;
            return this;
        }

        public INavigationOrchestrator Build()
        {
            NavigatorPlatform.EnsureInstalled();
            var views = NavigatorPlatform.ViewAdapter;

            var binder = new ViewBinder(_provider, _accessPolicy);
            var staticFactory = new StaticViewFactory(binder);
            var dynamicFactory = _dynamicFactory ?? CreateDefaultDynamicFactory();
            var orchestrator = new NavigationOrchestrator(staticFactory, dynamicFactory, _accessPolicy)
            {
                ClearHistoryOnMenuNavigation = _clearHistoryOnMenuNavigation ?? NavigatorPlatform.ClearHistoryOnMenuNavigationByDefault
            };

            if (_onFailed != null) orchestrator.NavigationFailed += (sender, e) => _onFailed(orchestrator, e);

            var defaultExclusions = new[]
            {
                typeof(BaseViewModel),
                typeof(BaseShellViewModel),
                typeof(BaseContentViewModel),
                typeof(BaseDialogViewModel)
            };
            var excluded = new HashSet<Type>(defaultExclusions.Concat(_excludedTypes));

            foreach (var assembly in _assemblies)
            {
                var viewModelTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("ViewModel", StringComparison.Ordinal))
                    .Where(t => !excluded.Contains(t))
                    .Where(t => typeof(BaseShellViewModel).IsAssignableFrom(t) || typeof(BaseContentViewModel).IsAssignableFrom(t));

                foreach (var vmType in viewModelTypes)
                {
                    var viewType = ViewLocator.ViewTypeFor(vmType);
                    if (viewType == null) continue;

                    var fits = typeof(BaseShellViewModel).IsAssignableFrom(vmType) ? views.IsShellView(viewType) : views.IsContentView(viewType);
                    if (!fits) continue;

                    staticFactory.Register(new ViewTypePair
                    {
                        ViewType = viewType,
                        ViewModelType = vmType
                    });
                }
            }

            return orchestrator;
        }

        private static IDynamicViewFactory CreateDefaultDynamicFactory()
        {
            var factory = NavigatorPlatform.DefaultDynamicViewFactory?.Invoke();
            return factory ?? new DynamicPages.NullDynamicViewFactory();
        }
    }
}
