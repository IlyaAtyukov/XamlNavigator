using System;
using XamlNavigator.Models;

namespace XamlNavigator.DynamicPages.FieldViewModels
{
    public class DateFieldViewModel : FieldViewModel
    {
        private DateTime? _value;

        public DateFieldViewModel(FieldDefinitionModel field) : base(field) { }

        public DateTime? Value
        {
            get { return _value; }
            set
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }

        public override object GetValue() => Value?.ToString("yyyy-MM-dd") ?? "";
        public override void SetValue(object value)
        {
            if (value is DateTime) { Value = (DateTime)value; return; }
            DateTime d;
            if (value != null && DateTime.TryParse(value.ToString(), out d)) { Value = d; return; }
            Value = null;
        }
        public override void Reset() => Value = null;
    }
}