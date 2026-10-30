using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace XamlNavigator.DynamicPages
{
    public class ReferenceLookupConverter : IValueConverter
    {
        private readonly DynamicPageViewModel _vm;
        private readonly string _propertyName;

        public ReferenceLookupConverter(DynamicPageViewModel vm, string propertyName)
        {
            _vm = vm;
            _propertyName = propertyName;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "";
            Dictionary<long, string> lookup;
            if (!_vm.ReferenceLookups.TryGetValue(_propertyName, out lookup)) return value.ToString() ?? "";
            var id = System.Convert.ToInt64(value);
            string s;
            return lookup.TryGetValue(id, out s) ? s : id.ToString();
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
}