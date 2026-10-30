using System.Collections.Generic;
using XamlNavigator.Models;

namespace XamlNavigator.DynamicPages.FieldViewModels
{
    public class ChoiceFieldViewModel : FieldViewModel
    {
        private string _selected;

        public ChoiceFieldViewModel(FieldDefinitionModel field, List<string> options) : base(field)  => Options = options;

        public List<string> Options { get; }
        public string Selected
        {
            get { return _selected; }
            set
            {
                _selected = value;
                OnPropertyChanged(nameof(Selected));
            }
        }

        public override object GetValue() => Selected ?? "";
        public override void SetValue(object value) => Selected = value?.ToString();
        public override void Reset() => Selected = null;
    }
}