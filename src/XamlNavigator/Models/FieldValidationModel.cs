using System;
using System.Globalization;
using System.Text.RegularExpressions;
using XamlNavigator.Localization;

namespace XamlNavigator.Models
{
    public enum TextRuleKind
    {
        None = 0,
        Email = 1,
        Phone = 2,
        LettersOnly = 3,
        Digits = 4,
        Custom = 5
    }

    public class FieldValidationModel
    {
        public double? Min { get; set; }

        public double? Max { get; set; }

        public TextRuleKind TextRule { get; set; }

        public string Pattern { get; set; }

        public string Message { get; set; }

        public bool IsEmpty => Min == null && Max == null && TextRule == TextRuleKind.None;

        private static readonly Regex EmailPattern = new Regex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.Compiled);

        private static readonly Regex PhonePattern = new Regex(@"^\+?(?:[\s\-()]*\d){5,15}[\s\-()]*$", RegexOptions.Compiled);

        private static readonly Regex LettersPattern = new Regex(@"^[\p{L}\s\-']+$", RegexOptions.Compiled);

        private static readonly Regex DigitsPattern = new Regex(@"^\d+$", RegexOptions.Compiled);

        public string Describe(FieldType fieldType)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (fieldType == FieldType.Number)
            {
                if (Min != null && Max != null) parts.Add(Tr.F("from {0} to {1}", Format(Min.Value), Format(Max.Value)));
                else if (Min != null) parts.Add(Tr.F("at least {0}", Format(Min.Value)));
                else if (Max != null) parts.Add(Tr.F("at most {0}", Format(Max.Value)));
            }
            else
            {
                if (Min != null && Max != null) parts.Add(Tr.F("{0} to {1} characters", Format(Min.Value), Format(Max.Value)));
                else if (Min != null) parts.Add(Tr.F("at least {0} characters", Format(Min.Value)));
                else if (Max != null) parts.Add(Tr.F("at most {0} characters", Format(Max.Value)));
            }

            switch (TextRule)
            {
                case TextRuleKind.Email: parts.Add(Tr.T("email address")); break;
                case TextRuleKind.Phone: parts.Add(Tr.T("phone number")); break;
                case TextRuleKind.LettersOnly: parts.Add(Tr.T("letters only")); break;
                case TextRuleKind.Digits: parts.Add(Tr.T("digits only")); break;
                case TextRuleKind.Custom: if (!string.IsNullOrWhiteSpace(Pattern)) parts.Add(Tr.T("matching the specified pattern")); break;
            }

            return parts.Count == 0 ? "" : string.Join(", ", parts);
        }

        public bool IsSatisfiedBy(object value, FieldType fieldType, out string error)
        {
            error = null;
            if (IsEmpty) return true;
            if (value == null) return true;

            if (fieldType == FieldType.Number)
            {
                double number;
                if (!TryToDouble(value, out number)) return true;

                if (Min != null && number < Min.Value) return Fail(Tr.F("a value of at least {0}", Format(Min.Value)), out error);
                if (Max != null && number > Max.Value) return Fail(Tr.F("a value of at most {0}", Format(Max.Value)), out error);
                return true;
            }

            var text = value as string;
            if (text == null) return true;
            if (text.Length == 0) return true;

            if (Min != null && text.Length < Min.Value) return Fail(Tr.F("at least {0} characters", Format(Min.Value)), out error);
            if (Max != null && text.Length > Max.Value) return Fail(Tr.F("at most {0} characters", Format(Max.Value)), out error);

            switch (TextRule)
            {
                case TextRuleKind.Email:
                    if (!EmailPattern.IsMatch(text)) return Fail(Tr.T("an email address, e.g. name@example.com"), out error);
                    break;
                case TextRuleKind.Phone:
                    if (!PhonePattern.IsMatch(text)) return Fail(Tr.T("a phone number, e.g. +1 555 123-4567"), out error);
                    break;
                case TextRuleKind.LettersOnly:
                    if (!LettersPattern.IsMatch(text)) return Fail(Tr.T("letters, spaces, and hyphens only"), out error);
                    break;
                case TextRuleKind.Digits:
                    if (!DigitsPattern.IsMatch(text)) return Fail(Tr.T("digits only"), out error);
                    break;
                case TextRuleKind.Custom:
                    if (string.IsNullOrWhiteSpace(Pattern)) break;
                    try
                    {
                        if (!Regex.IsMatch(text, ToFullMatchPattern(Pattern), RegexOptions.None, PatternTimeout))
                            return Fail(Tr.T("a value matching the specified pattern"), out error);
                    }
                    catch (ArgumentException) { return Fail(Tr.T("a valid pattern in the field definition (the current one is invalid)"), out error); }
                    catch (RegexMatchTimeoutException) { return Fail(Tr.T("a simpler pattern in the field definition: the current one takes too long to check"), out error); }
                    break;
            }

            return true;
        }

        public static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(250);

        /// <summary>Регулярное выражение, которому должно соответствовать всё значение целиком: ^(?:pattern)$.</summary>
        public static string ToFullMatchPattern(string pattern) => "^(?:" + pattern + ")$";

        /// <summary>Прежнее имя <see cref="ToFullMatchPattern"/>.</summary>
        [Obsolete("Use ToFullMatchPattern.")]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static string WholeValue(string pattern) => ToFullMatchPattern(pattern);

        private bool Fail(string expectation, out string error)
        {
            error = string.IsNullOrWhiteSpace(Message) ? Tr.F("expected {0}", expectation) : Message;
            return false;
        }

        private static bool TryToDouble(object value, out double result)
        {
            result = 0;
            if (value is double) { result = (double)value; return true; }
            if (value is float) { result = (float)value; return true; }
            if (value is decimal) { result = (double)(decimal)value; return true; }
            if (value is long) { result = (long)value; return true; }
            if (value is int) { result = (int)value; return true; }
            if (value is short) { result = (short)value; return true; }
            if (value is byte) { result = (byte)value; return true; }
            return false;
        }

        private static string Format(double value) =>
            value == Math.Floor(value) && Math.Abs(value) < 1e15
                ? ((long)value).ToString(CultureInfo.InvariantCulture)
                : value.ToString(CultureInfo.InvariantCulture);
    }
}
