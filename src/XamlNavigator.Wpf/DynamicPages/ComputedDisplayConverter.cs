using System;
using System.Globalization;
using System.Windows.Data;
using XamlNavigator.Models;

namespace XamlNavigator.DynamicPages
{
    public class ComputedDisplayConverter : IValueConverter
    {
        private readonly ComputedFieldModel _setup;

        public ComputedDisplayConverter(ComputedFieldModel setup) => _setup = setup;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => ComputedFieldEvaluator.Format(value, _setup);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}