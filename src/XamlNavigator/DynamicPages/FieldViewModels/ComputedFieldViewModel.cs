using XamlNavigator.Models;

namespace XamlNavigator.DynamicPages.FieldViewModels
{
    public class ComputedFieldViewModel : FieldViewModel
    {
        private object _value;

        public ComputedFieldViewModel(FieldDefinitionModel field) : base(field)
        {
            Computed = field.Computed;
        }

        public ComputedFieldModel Computed { get; }

        public object Value
        {
            get { return _value; }
            set
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
                OnPropertyChanged(nameof(Display));
            }
        }

        public string Display
        {
            get
            {
                var text = ComputedFieldEvaluator.Format(Value, Computed);
                return string.IsNullOrEmpty(text) ? "—" : text;
            }
        }

        public override object GetValue() => Value;

        public override void SetValue(object value) => Value = value;

        public override void Reset() => Value = null;
    }
}