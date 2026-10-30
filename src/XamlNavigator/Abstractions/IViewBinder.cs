using System.Collections.Generic;
using XamlNavigator.Core;
using XamlNavigator.Models;

namespace XamlNavigator.Abstractions
{
    public interface IViewBinder
    {
        ViewPair<object, BaseShellViewModel> CreateAndBindShellView(INavigationOrchestrator orchestrator, ViewTypePair model, List<ViewTypePair> childModels);
        ViewPair<object, BaseContentViewModel> CreateAndBindContentView(ViewTypePair model, INavigationOrchestrator orchestrator, BaseShellViewModel parent = null);
    }
}
