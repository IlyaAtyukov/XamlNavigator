using System;
using System.ComponentModel;
using XamlNavigator.Core;
using XamlNavigator.Models;

namespace XamlNavigator.DynamicPages.FieldViewModels
{
    /// <summary>Поле формы динамической страницы. Подпись — унаследованный <see cref="BaseViewModel.DisplayName"/>.</summary>
    public abstract class FieldViewModel : BaseViewModel
    {
        protected FieldViewModel(FieldDefinitionModel field)
        {
            DisplayName = field.DisplayName;
            IsRequired = field.IsRequired && field.FieldType != FieldType.Computed;
            PropertyName = field.PropertyName;
            FieldType = field.FieldType;
            Validation = field.Validation;
            Hint = field.Validation != null ? field.Validation.Describe(field.FieldType) : "";
        }

        /// <summary>Прежнее имя подписи поля.</summary>
        [Obsolete("Use DisplayName.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string FieldName => DisplayName;

        public bool IsRequired { get; }
        public string PropertyName { get; }
        public FieldType FieldType { get; }
        public FieldValidationModel Validation { get; }
        public string Hint { get; }
        public abstract object GetValue();
        public abstract void SetValue(object value);
        public abstract void Reset();
    }
}
