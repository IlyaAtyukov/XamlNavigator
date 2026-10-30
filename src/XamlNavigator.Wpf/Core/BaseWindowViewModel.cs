using System;
using XamlNavigator.Platform;

namespace XamlNavigator.Core
{
    /// <summary>Модель окна (Window), внутри которого меняются экраны. WPF-имя для <see cref="BaseShellViewModel"/>.</summary>
    public abstract class BaseWindowViewModel : BaseShellViewModel
    {
        static BaseWindowViewModel() => WpfPlatform.Install();

        /// <param name="startViewModelType">Модель экрана, который показать первым; null — первый пункт меню.</param>
        protected BaseWindowViewModel(Type startViewModelType = null) : base(startViewModelType) { }
    }

    /// <summary>Модель экрана (UserControl) внутри окна. WPF-имя для <see cref="BaseContentViewModel"/>.</summary>
    public abstract class BaseUserControlViewModel : BaseContentViewModel
    {
        static BaseUserControlViewModel() => WpfPlatform.Install();
    }
}
