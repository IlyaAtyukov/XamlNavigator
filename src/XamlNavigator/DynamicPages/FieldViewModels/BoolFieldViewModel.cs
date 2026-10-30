using XamlNavigator.Models;

namespace XamlNavigator.DynamicPages.FieldViewModels
{
    public class BoolFieldViewModel : FieldViewModel
    {
        private bool _value;

        public BoolFieldViewModel(FieldDefinitionModel field) : base(field) { }

        public bool Value
        {
            get { return _value; }
            set
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }

        public override object GetValue() => Value ? "true" : "false";
        public override void SetValue(object value) => Value = value is bool && (bool)value;
        public override void Reset() => Value = false;
    }
}