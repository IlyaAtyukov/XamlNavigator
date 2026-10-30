using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using XamlNavigator.Core;

namespace XamlNavigator.Models
{
    /// <summary>Пункт меню оболочки. <see cref="IsSelected"/> — это экран сейчас открыт.</summary>
    public class NavigationItem : ObservableObject
    {
        private bool _isSelected;

        public string DisplayName { get; set; } = "";
        public string Icon { get; set; }
        public bool HasIcon => !string.IsNullOrEmpty(Icon);
        public RelayCommand Command { get; set; }

        /// <summary>Номер группы меню (<see cref="NavigationCluster.Number"/>).</summary>
        public int Cluster { get; set; }
        public int Order { get; set; }

        /// <summary>Показан ли пункт: виден по замыслу ([ChildOf(ShowInMenu)]) и разрешён политикой доступа.</summary>
        public bool Visible { get; set; }

        /// <summary>Номер динамической страницы, на которую ведёт пункт; null — пункт обычного экрана.</summary>
        public long? DynamicPageId { get; set; }

        /// <summary>Прежнее имя <see cref="DynamicPageId"/>.</summary>
        [Obsolete("Use DynamicPageId.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public object Tag
        {
            get { return DynamicPageId; }
            set { DynamicPageId = value as long?; }
        }

        /// <summary>Модель экрана, на который ведёт пункт; null у динамических страниц.</summary>
        public Type ViewModelType { get; set; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetProperty(ref _isSelected, value); }
        }
    }

    /// <summary>
    /// Группа пунктов меню с одинаковым <see cref="NavigationItem.Cluster"/>. Группы идут по возрастанию номера;
    /// <see cref="IsFirst"/> — удобно прятать разделитель над первой группой.
    /// </summary>
    public class NavigationCluster : ObservableObject
    {
        private bool _isFirst;

        /// <summary>Номер группы из [ChildOf(Cluster = n)] или AddDynamicNavigationItem(cluster: n) — не позиция в списке.</summary>
        public int Number { get; set; }

        /// <summary>Группа стоит в меню первой.</summary>
        public bool IsFirst
        {
            get { return _isFirst; }
            set { SetProperty(ref _isFirst, value); }
        }

        /// <summary>Прежнее имя <see cref="Number"/>; позицией в списке не является.</summary>
        [Obsolete("This is the group number, not the position in the list. Use Number, or bind IsFirst.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int Index
        {
            get { return Number; }
            set { Number = value; }
        }

        public ObservableCollection<NavigationItem> Items { get; } = new ObservableCollection<NavigationItem>();
    }

    public class NavigationParameter<T>
    {
        public NavigationParameter(T value)
        {
            Value = value;
        }

        public T Value { get; }
    }

    /// <summary>Пара «представление + модель». В XAML: <c>{Binding CurrentView.View}</c>.</summary>
    public class ViewPair<TView, TViewModel> where TView : class where TViewModel : BaseViewModel
    {
        public TView View { get; set; }
        public TViewModel ViewModel { get; set; }
    }

    /// <summary>Зарегистрированная пара типов «представление ↔ модель».</summary>
    public class ViewTypePair
    {
        public Type ViewType { get; set; }
        public Type ViewModelType { get; set; }
    }

    /// <summary>Прежнее имя <see cref="ViewTypePair"/>.</summary>
    [Obsolete("Use ViewTypePair.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ViewTypeModel : ViewTypePair { }

    public class NavigationErrorEventArgs : EventArgs
    {
        public NavigationErrorEventArgs(string message, Exception exception = null)
        {
            Message = message;
            Exception = exception;
        }

        public string Message { get; }
        public Exception Exception { get; }

        /// <summary>Сообщение вместе с вложенными причинами: «A -> B -> C».</summary>
        public string FullMessage
        {
            get
            {
                var text = Message;
                for (var inner = Exception?.InnerException; inner != null; inner = inner.InnerException) text += " -> " + inner.Message;
                return text;
            }
        }
    }
}
