using System;

namespace XamlNavigator.Core
{
    /// <summary>Модель диалога. Представление ищется по имени: XxxDialogViewModel → XxxDialogView.</summary>
    public abstract class BaseDialogViewModel : BaseViewModel
    {
        public event Action<bool?> CloseRequested;
        protected void Close(bool? result = null) => CloseRequested?.Invoke(result);
    }
}
