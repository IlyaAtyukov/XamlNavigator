using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using XamlNavigator.Localization;
using XamlNavigator.Models;
using XamlNavigator.Platform;

namespace XamlNavigator.DynamicPages
{
    /// <summary>Номер записи из справочника → её название.</summary>
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
            long id;
            try { id = System.Convert.ToInt64(value, CultureInfo.InvariantCulture); }
            catch (Exception) { return value.ToString() ?? ""; }
            string s;
            return lookup.TryGetValue(id, out s) ? s : id.ToString(CultureInfo.InvariantCulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Значение расчётного поля → текст с округлением и единицей.</summary>
    public class ComputedDisplayConverter : IValueConverter
    {
        private readonly ComputedFieldModel _setup;

        public ComputedDisplayConverter(ComputedFieldModel setup) => _setup = setup;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => ComputedFieldEvaluator.Format(value, _setup);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Имя файла из папки «Фото» → картинка; параметр — высота.</summary>
    public class PhotoImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int height;
            if (!int.TryParse(System.Convert.ToString(parameter, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out height) || height <= 0) height = 48;
            var path = PhotoStore.Resolve(value as string);
            return path == null ? null : NavigatorPlatform.PhotoService?.LoadImage(path, height);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Непустая строка → true (для IsVisible).</summary>
    public class NotEmptyConverter : IValueConverter
    {
        public static readonly NotEmptyConverter Instance = new NotEmptyConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !string.IsNullOrWhiteSpace(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Подходит ли значение под правило подсветки → true (для DataTrigger).</summary>
    public class HighlightMatchConverter : IValueConverter
    {
        private readonly HighlightRuleModel _rule;

        public HighlightMatchConverter(HighlightRuleModel rule) => _rule = rule;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => _rule != null && _rule.Matches(value);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Цвета правила подсветки: разбор строки (#RRGGBB, #AARRGGBB, имя цвета) и читаемый цвет текста.</summary>
    public static class HighlightPainter
    {
        public static Color BackgroundFor(HighlightRuleModel rule) => rule == null || rule.IsEmpty ? null : ParseColor(rule.Background);

        public static Color ForegroundFor(HighlightRuleModel rule)
        {
            var background = BackgroundFor(rule);
            if (background == null) return null;
            return ParseColor(rule.Foreground) ?? ReadableOn(background);
        }

        public static Color ParseColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var text = value.Trim();

            Color color;
            if (Color.TryParse(text, out color)) return color;

            var named = typeof(Colors).GetField(text, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
            return named?.GetValue(null) as Color;
        }

        public static Color ReadableOn(Color background)
        {
            var luminance = 0.299 * background.Red + 0.587 * background.Green + 0.114 * background.Blue;
            return luminance > 0.55 ? Colors.Black : Colors.White;
        }
    }

    /// <summary>true/false → «Да»/«Нет», как в поиске и печати (DynamicPageViewModel.DisplayValue).</summary>
    public class BoolDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool ? ((bool)value ? Tr.T("Yes") : Tr.T("No")) : "";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
