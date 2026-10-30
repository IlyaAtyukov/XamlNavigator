using System.Collections.Generic;
using XamlNavigator.Localization;

namespace XamlNavigator.Models
{
    public enum ComputedKind { Arithmetic = 0, Aggregate = 1, Range = 2 }

    public enum ComputedOperation { Add = 0, Subtract = 1, Multiply = 2, Divide = 3 }

    public enum AggregateFunction { Sum = 0, Count = 1, Average = 2, Min = 3, Max = 4 }

    public class ComputedTerm
    {
        public ComputedOperation Operation { get; set; }
        public string Property { get; set; }
        public double Constant { get; set; }
        public bool IsConstant => string.IsNullOrWhiteSpace(Property);
    }

    public class ComputedRange
    {
        public double? From { get; set; }
        public double? To { get; set; }
        public double Value { get; set; }
    }

    public class ComputedFieldModel
    {
        public ComputedKind Kind { get; set; }
        public int Decimals { get; set; }
        public string Suffix { get; set; }
        public List<ComputedTerm> Terms { get; set; } = new List<ComputedTerm>();
        public AggregateFunction Function { get; set; }
        public string SourceTable { get; set; }
        public string SourceModelTypeName { get; set; }
        public string SourceForeignKey { get; set; }
        public string SourceValueProperty { get; set; }
        public string SourceTitle { get; set; }
        public string SourceValueTitle { get; set; }
        public string SourceDateProperty { get; set; }
        public int PeriodDays { get; set; }
        public string RangeSourceProperty { get; set; }
        public List<ComputedRange> Ranges { get; set; } = new List<ComputedRange>();
        public double Otherwise { get; set; }
        public string Describe(System.Func<string, string> titleOf)
        {
            switch (Kind)
            {
                case ComputedKind.Aggregate: return DescribeAggregate(titleOf);
                case ComputedKind.Range: return DescribeRange(titleOf);
                default: return DescribeArithmetic(titleOf);
            }
        }

        private string DescribeArithmetic(System.Func<string, string> titleOf)
        {
            if (Terms == null || Terms.Count == 0) return "";

            var text = "";
            for (var i = 0; i < Terms.Count; i++)
            {
                var term = Terms[i];
                if (i > 0) text += " " + Sign(term.Operation) + " ";
                text += term.IsConstant ? Number(term.Constant) : titleOf(term.Property);
            }
            return text;
        }

        private string DescribeAggregate(System.Func<string, string> titleOf)
        {
            var valueTitle = !string.IsNullOrWhiteSpace(SourceValueTitle) ? SourceValueTitle : SourceValueProperty;
            var sourceTitle = !string.IsNullOrWhiteSpace(SourceTitle) ? SourceTitle : SourceTable;

            var what = Function == AggregateFunction.Count
                ? Tr.T("number of records")
                : Tr.F("{0} of \"{1}\"", FunctionName(Function), valueTitle);

            var text = Tr.F("{0} in \"{1}\"", what, sourceTitle ?? "");
            if (PeriodDays > 0) text += Tr.F(" over the last {0} days", PeriodDays);
            return text;
        }

        private string DescribeRange(System.Func<string, string> titleOf)
        {
            var count = Ranges != null ? Ranges.Count : 0;
            return Tr.F("by the value of \"{0}\", ranges: {1}", titleOf(RangeSourceProperty), count);
        }

        public static string Sign(ComputedOperation operation)
        {
            switch (operation)
            {
                case ComputedOperation.Subtract: return "−";
                case ComputedOperation.Multiply: return "×";
                case ComputedOperation.Divide: return "÷";
                default: return "+";
            }
        }

        public static string FunctionName(AggregateFunction function)
        {
            switch (function)
            {
                case AggregateFunction.Count: return Tr.T("Count");
                case AggregateFunction.Average: return Tr.T("Average");
                case AggregateFunction.Min: return Tr.T("Minimum");
                case AggregateFunction.Max: return Tr.T("Maximum");
                default: return Tr.T("Sum");
            }
        }

        private static string Number(double value) =>
            value == System.Math.Floor(value) && System.Math.Abs(value) < 1e15
                ? ((long)value).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}