namespace XamlNavigator.Core
{
    /// <summary>Модель заглушки, которую оболочка показывает, когда открыть нечего.</summary>
    public class EmptyContentViewModel : BaseContentViewModel
    {
        public EmptyContentViewModel(string title, string message)
        {
            DisplayName = title;
            Message = message;
        }

        public string Message { get; }
    }
}
