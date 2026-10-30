using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace XamlNavigator.Models
{
    /// <summary>Своё представление записи для списка (ListViewMode.Custom): тип элемента и его привязываемые свойства.</summary>
    public class CustomViewInfo
    {
        public CustomViewInfo(Type controlType, string displayName, IEnumerable<CustomViewProperty> properties)
        {
            ControlType = controlType;
            DisplayName = displayName;
            Properties = (properties ?? Enumerable.Empty<CustomViewProperty>()).ToList();
        }

        public Type ControlType { get; }
        public string DisplayName { get; }

        /// <summary>Свойства элемента (DependencyProperty/BindableProperty), к которым привязываются поля сущности.</summary>
        public IReadOnlyList<CustomViewProperty> Properties { get; }

        /// <summary>Прежнее имя <see cref="Properties"/>.</summary>
        [Obsolete("Use Properties.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public IReadOnlyList<CustomViewProperty> Fields => Properties;

        public string Key => ControlType.FullName;
        public override string ToString() => DisplayName;
    }
}
