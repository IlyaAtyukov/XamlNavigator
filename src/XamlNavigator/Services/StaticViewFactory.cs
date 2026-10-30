using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using XamlNavigator.Abstractions;
using XamlNavigator.Attributes;
using XamlNavigator.Core;
using XamlNavigator.Localization;
using XamlNavigator.Models;
using XamlNavigator.Platform;

namespace XamlNavigator.Services
{
    public class StaticViewFactory : IStaticViewFactory
    {
        private readonly IViewBinder _binder;
        private readonly Dictionary<Type, ViewTypePair> _registry = new Dictionary<Type, ViewTypePair>();

        public StaticViewFactory(IViewBinder binder) => _binder = binder;

        public void Register(ViewTypePair model) => _registry[model.ViewModelType] = model;

        public bool IsRegistered(Type viewModelType) => _registry.ContainsKey(viewModelType);

        public IReadOnlyCollection<ViewTypePair> Registered => _registry.Values;

        public ViewPair<object, BaseShellViewModel> CreateShellView(Type viewModelType, INavigationOrchestrator orchestrator)
        {
            ViewTypePair model;
            if (!_registry.TryGetValue(viewModelType, out model)) throw new InvalidOperationException(Tr.F("No window (page) found for {0}.", viewModelType.Name));
            return _binder.CreateAndBindShellView(orchestrator, model, GetChildViewTypes(model.ViewModelType));
        }

        public ViewPair<object, BaseContentViewModel> CreateContentView(Type viewModelType, INavigationOrchestrator orchestrator)
        {
            ViewTypePair model;
            if (!_registry.TryGetValue(viewModelType, out model)) throw new InvalidOperationException(Tr.F("No screen found for {0}.", viewModelType.Name));
            return _binder.CreateAndBindContentView(model, orchestrator);
        }

        public List<ViewTypePair> GetChildViewTypes(Type parentViewModelType)
        {
            NavigatorPlatform.EnsureInstalled();
            var views = NavigatorPlatform.ViewAdapter;
            return _registry
                .Select(p => new
                {
                    Model = p.Value,
                    Attr = p.Key.GetCustomAttribute<ChildOfAttribute>()
                })
                .Where(x => x.Attr != null && x.Attr.ParentViewModelType == parentViewModelType && views.IsContentView(x.Model.ViewType))
                .OrderBy(x => x.Attr.Cluster)
                .ThenBy(x => x.Attr.Order)
                .Select(x => x.Model)
                .ToList();
        }
    }
}
