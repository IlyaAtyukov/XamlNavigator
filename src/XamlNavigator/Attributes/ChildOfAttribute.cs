using System;
using System.ComponentModel;

namespace XamlNavigator.Attributes
{
    /// <summary>
    /// Экран-содержимое принадлежит оболочке с моделью <see cref="ParentViewModelType"/>.
    /// Cluster и Order — группа и порядок в меню; ShowInMenu = false — экран есть, но пункта меню у него нет.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ChildOfAttribute : Attribute
    {
        public ChildOfAttribute(Type parentViewModelType)
        {
            ParentViewModelType = parentViewModelType;
        }

        public Type ParentViewModelType { get; }

        /// <summary>Номер группы меню; группы идут по возрастанию номера.</summary>
        public int Cluster { get; set; } = 0;

        /// <summary>Порядок внутри группы.</summary>
        public int Order { get; set; } = 0;

        /// <summary>Есть ли у экрана пункт меню. false — на экран переходят только из кода.</summary>
        public bool ShowInMenu { get; set; } = true;

        /// <summary>Прежнее имя <see cref="ShowInMenu"/>: экран при false не скрыт, у него просто нет пункта меню.</summary>
        [Obsolete("Use ShowInMenu.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool Visible
        {
            get { return ShowInMenu; }
            set { ShowInMenu = value; }
        }

        public string DisplayName { get; set; }
        public string IconKey { get; set; }
    }
}
