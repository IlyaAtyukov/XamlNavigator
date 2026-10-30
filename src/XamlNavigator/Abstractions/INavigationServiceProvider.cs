using System;

namespace XamlNavigator.Abstractions
{
    /// <summary>Откуда навигатор берёт зависимости моделей. Не нашлось — null.</summary>
    public interface INavigationServiceProvider
    {
        object GetService(Type serviceType);
        T GetService<T>() where T : class;
    }
}
