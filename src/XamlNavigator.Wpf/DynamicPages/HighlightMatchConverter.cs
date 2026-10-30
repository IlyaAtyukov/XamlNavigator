using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using XamlNavigator.Models;

namespace XamlNavigator.DynamicPages
{
    public class HighlightMatchConverter : IValueConverter
    {
        private readonly HighlightRuleModel _rule;

        public HighlightMatchConverter(HighlightRuleModel rule)
        {
            _rule = rule;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => _rule != null && _rule.Matches(value);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class CardHighlightConverter : IValueConverter
    {
        private readonly HighlightRuleModel _rule;
        private readonly Brush _brush;

        private readonly object _otherwise;

        /// <param name="rule">Правило подсветки.</param>
        /// <param name="brush">Кисть для записи, подходящей под правило.</param>
        /// <param name="otherwise">Значение, если запись под правило не подходит; по умолчанию прозрачная кисть.</param>
        public CardHighlightConverter(HighlightRuleModel rule, Brush brush, object otherwise = null)
        {
            _rule = rule;
            _brush = brush;
            _otherwise = otherwise ?? Brushes.Transparent;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => _rule != null && _rule.Matches(value) ? _brush : _otherwise;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public static class HighlightPainter
    {
        public static void Apply(DataGrid grid, HighlightRuleModel rule)
        {
            if (grid == null || rule == null || rule.IsEmpty) return;

            var background = ParseBrush(rule.Background);
            if (background == null) return;

            var foreground = ParseBrush(rule.Foreground) ?? ReadableOn(background);
            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.BasedOn = AppliedStyle(grid, DataGrid.CellStyleProperty, typeof(DataGridCell));

            var cellTrigger = new DataTrigger
            {
                Binding = new Binding(rule.PropertyName) { Converter = new HighlightMatchConverter(rule) },
                Value = true
            };
            cellTrigger.Setters.Add(new Setter(Control.BackgroundProperty, background));
            cellTrigger.Setters.Add(new Setter(Control.ForegroundProperty, foreground));
            cellStyle.Triggers.Add(cellTrigger);
            grid.CellStyle = cellStyle;

            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.BasedOn = AppliedStyle(grid, DataGrid.RowStyleProperty, typeof(DataGridRow));

            var rowTrigger = new DataTrigger
            {
                Binding = new Binding(rule.PropertyName) { Converter = new HighlightMatchConverter(rule) },
                Value = true
            };
            rowTrigger.Setters.Add(new Setter(Control.BackgroundProperty, background));
            rowStyle.Triggers.Add(rowTrigger);
            grid.RowStyle = rowStyle;
        }

        /// <summary>
        /// Неявный стиль для типа, видимый из <paramref name="scope"/>: ресурсы окна и страниц, если элемент уже
        /// в окне, иначе ресурсы приложения.
        /// </summary>
        public static Style FindStyle(FrameworkElement scope, Type targetType)
        {
            var style = scope?.TryFindResource(targetType) as Style;
            if (style == null) style = Application.Current?.TryFindResource(targetType) as Style;
            return style != null && style.TargetType.IsAssignableFrom(targetType) ? style : null;
        }

        /// <summary>
        /// Основа для своего стиля строки/ячейки: то, что задал стиль таблицы приложения (DataGrid.CellStyle и т. п.),
        /// иначе неявный стиль типа. Свой прежний локальный стиль снимаем, чтобы увидеть стиль приложения под ним.
        /// </summary>
        private static Style AppliedStyle(DataGrid grid, DependencyProperty styleProperty, Type targetType)
        {
            grid.ClearValue(styleProperty);
            return grid.GetValue(styleProperty) as Style ?? FindStyle(grid, targetType);
        }

        /// <summary>
        /// Стиль текста карточки: основа — неявный стиль TextBlock приложения, а у записи, подходящей под правило,
        /// цвет текста правила (или читаемый на его фоне). null — правила нет.
        /// </summary>
        public static Style TextStyleFor(HighlightRuleModel rule)
        {
            var background = BrushFor(rule);
            if (background == null) return null;

            var style = new Style(typeof(TextBlock)) { BasedOn = FindStyle(null, typeof(TextBlock)) };
            var trigger = new DataTrigger
            {
                Binding = new Binding(rule.PropertyName) { Converter = new HighlightMatchConverter(rule) },
                Value = true
            };
            trigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, ParseBrush(rule.Foreground) ?? ReadableOn(background)));
            style.Triggers.Add(trigger);
            return style;
        }

        public static SolidColorBrush BrushFor(HighlightRuleModel rule) => rule == null || rule.IsEmpty ? null : ParseBrush(rule.Background);

        public static SolidColorBrush ParseBrush(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            try
            {
                var converted = ColorConverter.ConvertFromString(value.Trim());
                if (converted == null) return null;
                var brush = new SolidColorBrush((Color)converted);
                brush.Freeze();
                return brush;
            }
            catch (Exception) { return null; }
        }

        public static SolidColorBrush ReadableOn(SolidColorBrush background)
        {
            var c = background.Color;
            var luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
            var brush = new SolidColorBrush(luminance > 0.55 ? Colors.Black : Colors.White);
            brush.Freeze();
            return brush;
        }
    }
}