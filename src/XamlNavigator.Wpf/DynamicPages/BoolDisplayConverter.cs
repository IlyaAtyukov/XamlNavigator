using System;
using System.Globalization;
using System.Windows.Data;
using XamlNavigator.Localization;

namespace XamlNavigator.DynamicPages
{
    /// <summary>Логическое поле в списке — «Да» / «Нет», как в поиске и печати.</summary>
    public class BoolDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool)) return "";
            return (bool)value ? Tr.T("Yes") : Tr.T("No");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
