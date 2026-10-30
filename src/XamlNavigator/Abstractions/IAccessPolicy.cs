using System;

namespace XamlNavigator.Abstractions
{
    /// <summary>Права доступа: какие экраны и динамические страницы видит текущий пользователь.</summary>
    public interface IAccessPolicy
    {
        bool CanAccess(Type viewModelType);
        bool CanAccessLevel(int requiredLevel);
    }
}
