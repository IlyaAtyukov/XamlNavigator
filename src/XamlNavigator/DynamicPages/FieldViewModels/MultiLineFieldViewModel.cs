using XamlNavigator.Models;

namespace XamlNavigator.DynamicPages.FieldViewModels
{
    public class MultiLineFieldViewModel : FieldViewModel
    {
        private string _value;
        public MultiLineFieldViewModel(FieldDefinitionModel field) :base(field) { }
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
        public override void SetValue(object value) => Value = value?.ToString() ?? "";
        public override void Reset() => Value = "";
    }
}