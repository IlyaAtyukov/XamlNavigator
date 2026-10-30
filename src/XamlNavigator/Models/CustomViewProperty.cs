using System;
using System.ComponentModel;

namespace XamlNavigator.Models
{
    /// <summary>
    /// Свойство своего представления записи (ListViewMode.Custom), к которому можно привязать поле сущности.
    /// <see cref="TargetProperty"/> — DependencyProperty в WPF, BindableProperty в MAUI.
    /// </summary>
    public class CustomViewProperty
    {
        public CustomViewProperty(string propertyName, string displayName, Type propertyType, object targetProperty)
        {
            PropertyName = propertyName;
            DisplayName = displayName;
            PropertyType = propertyType;
            TargetProperty = targetProperty;
        }

        public string PropertyName { get; }
        public string DisplayName { get; }
        public Type PropertyType { get; }

        /// <summary>DependencyProperty (WPF) или BindableProperty (MAUI).</summary>
        public object TargetProperty { get; }

        public override string ToString() => DisplayName;
    }

    /// <summary>Прежнее имя <see cref="CustomViewProperty"/>: «поле» здесь — свойство элемента, а не поле сущности.</summary>
    [Obsolete("Use CustomViewProperty.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class CustomViewField : CustomViewProperty
    {
        public CustomViewField(string propertyName, string displayName, Type propertyType, object targetProperty)
            : base(propertyName, displayName, propertyType, targetProperty) { }

        /// <summary>Прежнее имя <see cref="CustomViewProperty.TargetProperty"/> (DependencyProperty в WPF).</summary>
        public object DependencyProperty => TargetProperty;
    }
}
