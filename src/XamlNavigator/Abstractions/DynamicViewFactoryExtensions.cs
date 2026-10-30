using System;
using XamlNavigator.Core;

namespace XamlNavigator.Abstractions
{
    public static class DynamicViewFactoryExtensions
    {
        /// <summary>Зарегистрировать страницу, доступную всем (прежняя форма Register без уровня доступа).</summary>
        public static void Register(this IDynamicViewFactory factory, long pageId, Func<BaseContentViewModel> viewModelFactory) =>
            factory.Register(pageId, viewModelFactory, 0);
    }
}
