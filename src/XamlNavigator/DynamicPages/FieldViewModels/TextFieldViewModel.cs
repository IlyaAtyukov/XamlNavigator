using XamlNavigator.Models;

namespace XamlNavigator.DynamicPages.FieldViewModels
{
    public class TextFieldViewModel : FieldViewModel
    {
        public TextFieldViewModel(FieldDefinitionModel field) : base(field) {}
        
        private string _value;  
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