using System;
using System.Collections.Generic;
using XamlNavigator.Core;
using XamlNavigator.Models;

namespace XamlNavigator.Abstractions
{
    /// <summary>Реестр «модель → представление», заполняется NavigatorBuilder при сканировании сборок.</summary>
    public interface IStaticViewFactory
    {
        void Register(ViewTypePair model);
        bool IsRegistered(Type viewModelType);

        ViewPair<object, BaseShellViewModel> CreateShellView(Type viewModelType, INavigationOrchestrator orchestrator);
        ViewPair<object, BaseContentViewModel> CreateContentView(Type viewModelType, INavigationOrchestrator orchestrator);

        List<ViewTypePair> GetChildViewTypes(Type parentViewModelType);
    }
}
