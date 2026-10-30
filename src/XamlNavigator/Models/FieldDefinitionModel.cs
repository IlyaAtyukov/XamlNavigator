using System;
using System.ComponentModel;
using Newtonsoft.Json;

namespace XamlNavigator.Models
{
    /// <summary>Описание поля динамической страницы: что показать, к какому свойству модели привязать, как проверять.</summary>
    public class FieldDefinitionModel
    {
        public long Id { get; set; }
        public long PageId { get; set; }

        /// <summary>
        /// Подпись поля для пользователя: заголовок столбца, метка в форме, имя в сообщениях проверки.
        /// В JSON — под прежним именем «FieldName», чтобы сохранённые описания читались как раньше.
        /// </summary>
        [JsonProperty("FieldName")]
        public string DisplayName { get; set; } = "";

        /// <summary>Прежнее имя <see cref="DisplayName"/>.</summary>
        [Obsolete("Use DisplayName: this is the caption shown to the user, not an identifier.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [JsonIgnore]
        public string FieldName
        {
            get { return DisplayName; }
            set { DisplayName = value; }
        }

        /// <summary>Свойство модели (CLR), в которое пишется значение поля.</summary>
        public string PropertyName { get; set; } = "";
        public FieldType FieldType { get; set; } = FieldType.Text;
        public bool IsRequired { get; set; }
        public int SortOrder { get; set; }
        public bool ShowInList { get; set; } = true;

        /// <summary>Варианты поля <see cref="FieldType.Choice"/>: текст JSON-массива строк, например ["шт","кг"].</summary>
        [JsonProperty("Options")]
        public string OptionsJson { get; set; }

        /// <summary>Прежнее имя <see cref="OptionsJson"/>.</summary>
        [Obsolete("Use OptionsJson: the value is the text of a JSON array.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [JsonIgnore]
        public string Options
        {
            get { return OptionsJson; }
            set { OptionsJson = value; }
        }

        public string ReferenceTable { get; set; }
        public string ReferenceModelTypeName { get; set; }
        public string ReferenceTitleProperty { get; set; }
        public FieldValidationModel Validation { get; set; }
        public ComputedFieldModel Computed { get; set; }
    }

    public enum FieldType
    {
        Text,
        MultiLine,
        Number,
        Date,
        Bool,

        /// <summary>Выбор одного значения из фиксированного списка (<see cref="FieldDefinitionModel.OptionsJson"/>).</summary>
        Choice,

        Reference,
        Computed,
        Photo,

        /// <summary>Прежнее имя <see cref="Choice"/> (то же значение 5).</summary>
        [Obsolete("Use Choice.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        List = Choice
    }
}
