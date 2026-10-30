using System;
using System.Collections.Generic;
using System.Linq;
using XamlNavigator.Abstractions;
using XamlNavigator.Core;
using XamlNavigator.Localization;
using XamlNavigator.Models;
using XamlNavigator.Platform;

namespace XamlNavigator.Services
{
    /// <summary>
    /// Создаёт представление и модель и связывает их. Зависимости модели берутся из контейнера по самому
    /// длинному конструктору; INavigationOrchestrator подставляется любой модели.
    /// </summary>
    public class ViewBinder : IViewBinder
    {
        private readonly INavigationServiceProvider _provider;
        private readonly IAccessPolicy _accessPolicy;

        public ViewBinder(INavigationServiceProvider provider, IAccessPolicy accessPolicy = null)
        {
            _provider = provider;
            _accessPolicy = accessPolicy;
        }

        private static IViewAdapter Views
        {
            get
            {
                NavigatorPlatform.EnsureInstalled();
                return NavigatorPlatform.ViewAdapter;
            }
        }

        public ViewPair<object, BaseShellViewModel> CreateAndBindShellView(INavigationOrchestrator orchestrator, ViewTypePair model, List<ViewTypePair> childModels)
        {
            var views = Views;
            if (!views.IsShellView(model.ViewType)) throw new InvalidOperationException(Tr.F("{0} can't be used as a window (page).", model.ViewType.Name));
            var view = views.CreateView(model.ViewType);

            var viewModel = CreateInstance(model.ViewModelType, orchestrator) as BaseShellViewModel;
            if (viewModel == null) throw new InvalidOperationException(Tr.F("{0} does not inherit from BaseShellViewModel.", model.ViewModelType.Name));
            viewModel.AccessPolicy = _accessPolicy;

            // Сначала экраны и меню, потом привязка: иначе привязки к меню вычислятся по пустому списку.
            foreach (var child in childModels)
            {
                var childModel = child;
                viewModel.RegisterChild(() => CreateAndBindContentView(childModel, orchestrator, viewModel), childModel.ViewModelType);
            }

            views.Bind(view, viewModel);
            views.AttachShell(view, viewModel, orchestrator);

            return new ViewPair<object, BaseShellViewModel> { View = view, ViewModel = viewModel };
        }

        public ViewPair<object, BaseContentViewModel> CreateAndBindContentView(ViewTypePair model, INavigationOrchestrator orchestrator, BaseShellViewModel parent = null)
        {
            var views = Views;
            if (!views.IsContentView(model.ViewType)) throw new InvalidOperationException(Tr.F("{0} can't be used as a screen inside a window (page).", model.ViewType.Name));
            var view = views.CreateView(model.ViewType);

            var viewModel = CreateInstance(model.ViewModelType, orchestrator) as BaseContentViewModel;
            if (viewModel == null) throw new InvalidOperationException(Tr.F("{0} does not inherit from BaseContentViewModel.", model.ViewModelType.Name));

            views.Bind(view, viewModel);
            if (parent != null) viewModel.SetParentShell(parent);

            return new ViewPair<object, BaseContentViewModel> { View = view, ViewModel = viewModel };
        }

        /// <summary>Создать объект, взяв параметры самого длинного конструктора из контейнера.</summary>
        public object CreateInstance(Type type, INavigationOrchestrator orchestrator = null) => Create(type, _provider, orchestrator, _accessPolicy);

        /// <summary>
        /// Создать объект по самому длинному конструктору: параметры — из <paramref name="provider"/>,
        /// INavigationOrchestrator и IAccessPolicy — переданные, необязательные — значения по умолчанию.
        /// </summary>
        public static object Create(Type type, INavigationServiceProvider provider, INavigationOrchestrator orchestrator = null, IAccessPolicy accessPolicy = null)
        {
            var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            if (constructor == null) throw new InvalidOperationException(Tr.F("{0} has no public constructor.", type.Name));

            var parameters = constructor.GetParameters().Select(p =>
            {
                if (p.ParameterType == typeof(INavigationOrchestrator) && orchestrator != null) return orchestrator;

                var service = provider?.GetService(p.ParameterType);
                if (service != null) return service;

                if (p.ParameterType == typeof(IAccessPolicy) && accessPolicy != null) return accessPolicy;
                if (p.IsOptional) return DefaultOf(p);
                throw new InvalidOperationException(Tr.F("{0}: service {1} is not registered.", type.Name, p.ParameterType.Name));
            }).ToArray();

            var instance = Activator.CreateInstance(type, parameters);
            if (instance == null) throw new InvalidOperationException(Tr.F("Couldn't create {0}.", type.Name));
            return instance;
        }

        /// <summary>
        /// Значение по умолчанию необязательного параметра. Для <c>DateTime d = default</c> и подобных .NET Framework
        /// отдаёт null, DBNull или вовсе бросает FormatException — значимому типу тогда нужен его нулевой экземпляр.
        /// </summary>
        private static object DefaultOf(System.Reflection.ParameterInfo p)
        {
            object value;
            try { value = p.HasDefaultValue ? p.DefaultValue : null; }
            catch (FormatException) { value = null; }
            if (value == null || value is DBNull || value == System.Reflection.Missing.Value)
                return p.ParameterType.IsValueType && Nullable.GetUnderlyingType(p.ParameterType) == null
                    ? Activator.CreateInstance(p.ParameterType)
                    : null;
            var enumType = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;
            if (enumType.IsEnum && !enumType.IsInstanceOfType(value)) return Enum.ToObject(enumType, value);
            return value;
        }
    }
}
