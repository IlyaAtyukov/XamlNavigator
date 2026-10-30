using System;
using System.Globalization;
using XamlNavigator.Models;

namespace XamlNavigator.DynamicPages.FieldViewModels
{
    public class NumberFieldViewModel : FieldViewModel
    {
        private string _value;
        public NumberFieldViewModel(FieldDefinitionModel field) : base(field){}

        public string Value
        {
            get { return _value; }
            set
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }

        public override object GetValue() => Value;

        public override void SetValue(object value) => Value = value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture);

        public override void Reset() => Value = "";
    }
}