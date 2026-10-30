using System;
using XamlNavigator.Core;
using XamlNavigator.Models;

namespace XamlNavigator.Abstractions
{
    public interface IDynamicViewFactory
    {
        void Register(long pageId, Func<BaseContentViewModel> viewModelFactory, int requiredAccessLevel);
        void Unregister(long pageId);
        bool IsRegistered(long pageId);
        int GetRequiredAccessLevel(long pageId);

        /// <summary>Создать новую модель и представление страницы (каждый вызов — новая пара).</summary>
        ViewPair<object, BaseContentViewModel> CreateView(long pageId);
    }
}
