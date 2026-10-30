using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using XamlNavigator.Localization;
using XamlNavigator.Models;

namespace XamlNavigator.DynamicPages
{
    public class ComputedFieldEvaluator
    {
        private readonly Type _modelType;
        private readonly List<FieldDefinitionModel> _computed;

        private readonly Dictionary<string, Dictionary<long, double>> _aggregates = new Dictionary<string, Dictionary<long, double>>();

        private bool _dividedByZero;

        private readonly List<string> _problems = new List<string>();

        private readonly HashSet<string> _missing = new HashSet<string>();

        public ComputedFieldEvaluator(Type modelType, IEnumerable<FieldDefinitionModel> fields)
        {
            _modelType = modelType;
            _computed = Order(fields
                .Where(f => f.FieldType == FieldType.Computed && f.Computed != null)
                .OrderBy(f => f.SortOrder)
                .ToList());
            // Круговые ссылки находятся один раз, а список проблем пересобирается при каждом подсчёте.
            _orderProblems = _problems.ToList();
        }

        private readonly List<string> _orderProblems;

        public bool HasComputedFields => _computed.Count > 0;

        private List<FieldDefinitionModel> Order(List<FieldDefinitionModel> fields)
        {
            var result = new List<FieldDefinitionModel>();
            var pending = new List<FieldDefinitionModel>(fields);
            var done = new HashSet<string>();

            while (pending.Count > 0)
            {
                var ready = pending.FirstOrDefault(f => DependsOn(f).All(d => !pending.Any(p => p.PropertyName == d) || done.Contains(d)));
                if (ready == null)
                {
                    foreach (var field in pending)
                        Problem(field, Tr.T("the formula refers back to itself (circular reference)"));
                    result.AddRange(pending);
                    break;
                }

                result.Add(ready);
                done.Add(ready.PropertyName);
                pending.Remove(ready);
            }

            return result;
        }

        private static IEnumerable<string> DependsOn(FieldDefinitionModel field)
        {
            var setup = field.Computed;
            if (setup.Kind == ComputedKind.Range)
            {
                if (!string.IsNullOrWhiteSpace(setup.RangeSourceProperty)) yield return setup.RangeSourceProperty;
                yield break;
            }

            if (setup.Kind != ComputedKind.Arithmetic) yield break;
            foreach (var term in setup.Terms ?? new List<ComputedTerm>())
                if (!term.IsConstant) yield return term.Property;
        }

        public IReadOnlyList<string> Problems => _problems;

        public void PrepareAggregates(Func<string, string, List<object>> loadRows) =>
            PrepareAggregatesAsync((table, typeName) => Task.FromResult(loadRows(table, typeName))).GetAwaiter().GetResult();

        /// <summary>
        /// Посчитать агрегаты по связанным таблицам. <paramref name="loadRows"/> получает имя таблицы и
        /// полное имя типа модели и возвращает строки (null — таблицы нет).
        /// </summary>
        public async Task PrepareAggregatesAsync(Func<string, string, Task<List<object>>> loadRows)
        {
            _aggregates.Clear();
            _problems.Clear();
            _problems.AddRange(_orderProblems);

            foreach (var field in _computed.Where(f => f.Computed.Kind == ComputedKind.Aggregate))
            {
                var setup = field.Computed;
                try
                {
                    if (string.IsNullOrWhiteSpace(setup.SourceTable) || string.IsNullOrWhiteSpace(setup.SourceForeignKey))
                    {
                        Problem(field, Tr.T("no related table selected"));
                        continue;
                    }

                    var rows = await loadRows(setup.SourceTable, setup.SourceModelTypeName);
                    if (rows == null)
                    {
                        Problem(field, Tr.F("table \"{0}\" not found", setup.SourceTable));
                        continue;
                    }

                    _aggregates[field.PropertyName] = Aggregate(field, rows, setup);
                }
                catch (Exception ex) { Problem(field, ex.Message); }
            }
        }

        private Dictionary<long, double> Aggregate(FieldDefinitionModel field, List<object> rows, ComputedFieldModel setup)
        {
            var result = new Dictionary<long, double>();
            if (rows.Count == 0) return result;

            var rowType = rows[0].GetType();
            var fkProp = rowType.GetProperty(setup.SourceForeignKey);
            if (fkProp == null)
            {
                Problem(field, Tr.F("table \"{0}\" has no field \"{1}\"", setup.SourceTable, setup.SourceForeignKey));
                return result;
            }

            PropertyInfo valueProp = null;
            if (setup.Function != AggregateFunction.Count)
            {
                valueProp = rowType.GetProperty(setup.SourceValueProperty ?? "");
                if (valueProp == null)
                {
                    Problem(field, Tr.F("table \"{0}\" has no field \"{1}\"", setup.SourceTable, setup.SourceValueProperty));
                    return result;
                }
            }

            PropertyInfo dateProp = null;
            DateTime since = DateTime.MinValue;
            if (setup.PeriodDays > 0 && !string.IsNullOrWhiteSpace(setup.SourceDateProperty))
            {
                dateProp = rowType.GetProperty(setup.SourceDateProperty);
                if (dateProp == null) Problem(field, Tr.F("table \"{0}\" has no date field \"{1}\"", setup.SourceTable, setup.SourceDateProperty));
                else since = DateTime.Now.Date.AddDays(-(setup.PeriodDays - 1));
            }

            var buckets = new Dictionary<long, List<double>>();

            foreach (var row in rows)
            {
                var ownerId = ToLong(fkProp.GetValue(row));
                if (ownerId == 0) continue;

                if (dateProp != null)
                {
                    var when = dateProp.GetValue(row) as DateTime?;
                    if (when == null || when.Value.Date < since) continue;
                }

                double value = 1;
                if (valueProp != null && !TryToDouble(valueProp.GetValue(row), out value)) continue;

                List<double> bucket;
                if (!buckets.TryGetValue(ownerId, out bucket)) buckets[ownerId] = bucket = new List<double>();
                bucket.Add(value);
            }

            foreach (var pair in buckets) result[pair.Key] = Reduce(setup.Function, pair.Value);
            return result;
        }

        private static double Reduce(AggregateFunction function, List<double> values)
        {
            switch (function)
            {
                case AggregateFunction.Count: return values.Count;
                case AggregateFunction.Average: return values.Count == 0 ? 0 : values.Average();
                case AggregateFunction.Min: return values.Count == 0 ? 0 : values.Min();
                case AggregateFunction.Max: return values.Count == 0 ? 0 : values.Max();
                default: return values.Sum();
            }
        }

        public void Apply(object record) => Apply(record, true);

        public void Apply(object record, bool includeAggregates)
        {
            if (record == null) return;

            foreach (var field in _computed)
            {
                if (!includeAggregates && field.Computed.Kind == ComputedKind.Aggregate) continue;

                _missing.Clear();
                _dividedByZero = false;
                try
                {
                    var prop = _modelType.GetProperty(field.PropertyName);
                    if (prop == null || !prop.CanWrite) continue;
                    object value;
                    if (TryConvert(Compute(field, record), prop.PropertyType, out value)) prop.SetValue(record, value);

                    if (_missing.Count > 0)
                        Problem(field, Tr.F("the formula refers to a field that no longer exists: \"{0}\"", string.Join("», «", _missing)));
                    else if (_dividedByZero)
                        Problem(field, Tr.T("division by zero — value not calculated"));
                }
                catch (Exception ex) { Problem(field, ex.Message); }
            }
        }

        /// <summary>
        /// Результат расчёта в тип свойства: decimal, int, float… Пустой результат в ненулевой тип — нуль этого
        /// типа: оставить прежнее значение значило бы выдать устаревший расчёт за текущий (о пустоте сообщает Problem).
        /// </summary>
        private static bool TryConvert(double? result, Type propertyType, out object value)
        {
            var target = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (result == null)
            {
                var nonNullable = propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) == null;
                value = nonNullable ? Activator.CreateInstance(propertyType) : null;
                return true;
            }

            if (target == typeof(double)) value = result.Value;
            // Строка — в инвариантной культуре: так её читает правило подсветки (HighlightRuleModel).
            else if (target == typeof(string)) value = result.Value.ToString(CultureInfo.InvariantCulture);
            else if (target == typeof(decimal)) value = (decimal)result.Value;
            else if (target == typeof(int) || target == typeof(long) || target == typeof(short) || target == typeof(byte))
                value = Convert.ChangeType(Math.Round(result.Value, MidpointRounding.AwayFromZero), target, CultureInfo.InvariantCulture);
            else value = Convert.ChangeType(result.Value, target, CultureInfo.InvariantCulture);
            return true;
        }

        private double? Compute(FieldDefinitionModel field, object record)
        {
            var setup = field.Computed;
            switch (setup.Kind)
            {
                case ComputedKind.Aggregate:
                    {
                        Dictionary<long, double> byOwner;
                        if (!_aggregates.TryGetValue(field.PropertyName, out byOwner)) return null;

                        var idProp = _modelType.GetProperty("Id");
                        var id = ToLong(idProp?.GetValue(record));
                        if (id == 0) return null;

                        double value;
                        return byOwner.TryGetValue(id, out value) ? value : 0;
                    }

                case ComputedKind.Range:
                    {
                        double source;
                        if (!TryReadNumber(record, setup.RangeSourceProperty, out source)) return null;

                        foreach (var range in setup.Ranges ?? new List<ComputedRange>())
                        {
                            if (range.From != null && source < range.From.Value) continue;
                            if (range.To != null && source >= range.To.Value) continue;
                            return range.Value;
                        }
                        return setup.Otherwise;
                    }

                default:
                    {
                        var terms = setup.Terms ?? new List<ComputedTerm>();
                        if (terms.Count == 0) return null;

                        double result;
                        if (!TryTerm(record, terms[0], out result)) return null;

                        for (var i = 1; i < terms.Count; i++)
                        {
                            double operand;
                            if (!TryTerm(record, terms[i], out operand)) return null;

                            switch (terms[i].Operation)
                            {
                                case ComputedOperation.Subtract: result -= operand; break;
                                case ComputedOperation.Multiply: result *= operand; break;
                                case ComputedOperation.Divide:
                                    if (Math.Abs(operand) < double.Epsilon) { _dividedByZero = true; return null; }
                                    result /= operand;
                                    break;
                                default: result += operand; break;
                            }
                        }
                        return result;
                    }
            }
        }

        private bool TryTerm(object record, ComputedTerm term, out double value)
        {
            if (term.IsConstant)
            {
                value = term.Constant;
                return true;
            }
            return TryReadNumber(record, term.Property, out value);
        }

        private bool TryReadNumber(object record, string propertyName, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(propertyName)) return false;

            var prop = _modelType.GetProperty(propertyName);
            if (prop == null)
            {
                _missing.Add(propertyName);
                return false;
            }

            var raw = prop.GetValue(record);
            if (raw == null) return false;

            return TryToDouble(raw, out value);
        }

        public static string Format(object value, ComputedFieldModel setup)
        {
            double number;
            if (value == null || !TryToDouble(value, out number)) return "";
            if (setup == null) return number.ToString(CultureInfo.CurrentCulture);

            var decimals = Math.Max(0, Math.Min(6, setup.Decimals));
            var text = number.ToString("N" + decimals, CultureInfo.CurrentCulture);
            return string.IsNullOrWhiteSpace(setup.Suffix) ? text : text + " " + setup.Suffix.Trim();
        }

        private void Problem(FieldDefinitionModel field, string reason)
        {
            var text = $"«{field.DisplayName}»: {reason}";
            if (!_problems.Contains(text)) _problems.Add(text);
        }

        private static long ToLong(object raw)
        {
            if (raw == null) return 0;
            try { return Convert.ToInt64(raw, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        private static bool TryToDouble(object raw, out double value)
        {
            value = 0;
            if (raw == null) return false;

            if (raw is double) { value = (double)raw; return true; }
            if (raw is decimal) { value = (double)(decimal)raw; return true; }
            if (raw is float) { value = (float)raw; return true; }
            if (raw is long) { value = (long)raw; return true; }
            if (raw is int) { value = (int)raw; return true; }
            if (raw is short) { value = (short)raw; return true; }
            if (raw is byte) { value = (byte)raw; return true; }

            var text = raw as string;
            if (text == null) return false;

            var cleaned = new string(text.Where(c => char.IsDigit(c) || c == ',' || c == '.' || c == '-').ToArray())
                .Replace(',', '.');
            return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}