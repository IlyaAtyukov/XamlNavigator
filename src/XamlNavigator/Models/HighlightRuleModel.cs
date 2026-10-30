using System;
using System.Globalization;

namespace XamlNavigator.Models
{
    public enum HighlightComparison
    {
        Greater = 0,
        GreaterOrEqual = 1,
        Less = 2,
        LessOrEqual = 3,
        Equal = 4,
        NotEqual = 5
    }

    public class HighlightRuleModel
    {
        public string PropertyName { get; set; }
        public HighlightComparison Comparison { get; set; }
        public string Value { get; set; }
        public string Background { get; set; }
        public string Foreground { get; set; }
        public bool IsEmpty => string.IsNullOrWhiteSpace(PropertyName) || string.IsNullOrWhiteSpace(Background);

        public bool Matches(object raw)
        {
            if (IsEmpty) return false;

            double threshold;
            double number;
            if (TryThreshold(Value, out threshold) && TryNumber(raw, out number))
                return Compare(number.CompareTo(threshold));

            var text = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
            return Compare(string.Compare(text, Value ?? "", StringComparison.CurrentCultureIgnoreCase));
        }

        private bool Compare(int sign)
        {
            switch (Comparison)
            {
                case HighlightComparison.Greater: return sign > 0;
                case HighlightComparison.GreaterOrEqual: return sign >= 0;
                case HighlightComparison.Less: return sign < 0;
                case HighlightComparison.LessOrEqual: return sign <= 0;
                case HighlightComparison.NotEqual: return sign != 0;
                default: return sign == 0;
            }
        }

        /// <summary>Порог, как его вводят руками: «1,5», «1.5», «1 500» — запятая дробная, пробелы разрядные.</summary>
        private static bool TryThreshold(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var normalized = text.Trim().Replace("\u00A0", "").Replace("\u202F", "").Replace(" ", "").Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryNumber(object raw, out double value)
        {
            value = 0;
            if (raw == null) return false;
            if (raw is bool) { value = (bool)raw ? 1 : 0; return true; }

            try
            {
                value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception) { return false; }
        }

        public static string SignOf(HighlightComparison comparison)
        {
            switch (comparison)
            {
                case HighlightComparison.GreaterOrEqual: return "≥";
                case HighlightComparison.Less: return "<";
                case HighlightComparison.LessOrEqual: return "≤";
                case HighlightComparison.Equal: return "=";
                case HighlightComparison.NotEqual: return "≠";
                default: return ">";
            }
        }
    }
}