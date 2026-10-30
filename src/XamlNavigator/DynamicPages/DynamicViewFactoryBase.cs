using System;
using System.Collections.Generic;
using XamlNavigator.Abstractions;
using XamlNavigator.Core;
using XamlNavigator.Localization;
using XamlNavigator.Models;
using XamlNavigator.Platform;

namespace XamlNavigator.DynamicPages
{
    /// <summary>
    /// Реестр динамических страниц: номер страницы → фабрика модели и требуемый уровень доступа.
    /// Само представление строит платформа в <see cref="BuildView"/>.
    /// </summary>
    public abstract class DynamicViewFactoryBase : IDynamicViewFactory
    {
        private readonly Dictionary<long, Func<BaseContentViewModel>> _factories = new Dictionary<long, Func<BaseContentViewModel>>();
        private readonly Dictionary<long, int> _requiredLevels = new Dictionary<long, int>();

        public void Register(long pageId, Func<BaseContentViewModel> viewModelFactory, int requiredAccessLevel)
        {
            _factories[pageId] = viewModelFactory;
            _requiredLevels[pageId] = requiredAccessLevel;
        }

        public void Unregister(long pageId)
        {
            _factories.Remove(pageId);
            _requiredLevels.Remove(pageId);
        }

        public bool IsRegistered(long pageId) => _factories.ContainsKey(pageId);

        public int GetRequiredAccessLevel(long pageId)
        {
            int level;
            return _requiredLevels.TryGetValue(pageId, out level) ? level : 0;
        }

        public ViewPair<object, BaseContentViewModel> CreateView(long pageId)
        {
            Func<BaseContentViewModel> factory;
            if (!_factories.TryGetValue(pageId, out factory)) throw new InvalidOperationException(Tr.F("Dynamic page {0} is not registered.", pageId));

            var vm = factory();
            var view = BuildView(vm);
            if (view != null) NavigatorPlatform.ViewAdapter?.Bind(view, vm);
            return new ViewPair<object, BaseContentViewModel> { View = view, ViewModel = vm };
        }

        /// <summary>Построить представление для модели динамической страницы (обычно <see cref="DynamicPageViewModel"/>).</summary>
        protected abstract object BuildView(BaseContentViewModel viewModel);
    }

    /// <summary>
    /// Фабрика без представлений («пустой объект»): страницы регистрируются и открываются, но показать их нечем.
    /// Используется по умолчанию, если платформа не дала своей, и в тестах.
    /// </summary>
    public sealed class NullDynamicViewFactory : DynamicViewFactoryBase
    {
        protected override object BuildView(BaseContentViewModel viewModel) => null;
    }
}
