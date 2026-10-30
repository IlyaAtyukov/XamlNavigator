using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using XamlNavigator.Core;
using XamlNavigator.DynamicPages;
using XamlNavigator.Localization;
using XamlNavigator.Models;
using XamlNavigator.Platform;
using Xunit;

namespace XamlNavigator.Tests
{
    public class CoreTests
    {
        public CoreTests() => TestPlatform.Install();

        [Fact]
        public async Task Async_command_is_blocked_while_running_and_reports_exceptions()
        {
            var gate = new TaskCompletionSource<bool>();
            var command = new RelayCommand(async _ => { await gate.Task; throw new InvalidOperationException("сбой"); });
            Exception reported = null;
            EventHandler<Exception> onFailed = (s, e) => { if (s == command) reported = e; };
            RelayCommand.ExecutionFailed += onFailed;
            try
            {
                var running = command.ExecuteAsync();
                Assert.False(command.CanExecute(null));
                Assert.True(command.IsExecuting);

                gate.SetResult(true);
                Assert.False(await running);
                Assert.True(command.CanExecute(null));
                Assert.Equal("сбой", reported?.Message);
            }
            finally { RelayCommand.ExecutionFailed -= onFailed; }
        }

        [Fact]
        public void Explicit_requery_notifies_live_commands()
        {
            var requery = new ExplicitCommandRequery();
            var command = new RelayCommand(_ => { });
            var raised = 0;
            EventHandler handler = (s, e) => raised++;

            requery.Subscribe(command, handler);
            requery.InvalidateAll();
            requery.Invalidate(command);
            requery.Unsubscribe(command, handler);
            requery.InvalidateAll();

            Assert.Equal(2, raised);
        }

        [Fact]
        public void Requery_subscriptions_move_to_the_platform_installed_later()
        {
            var early = new ExplicitCommandRequery();
            var later = new ExplicitCommandRequery();
            var command = new RelayCommand(_ => { });
            var raised = 0;
            early.Subscribe(command, (s, e) => raised++);

            early.TransferTo(later);
            later.Invalidate(command);
            early.Invalidate(command);

            Assert.Equal(1, raised);
        }

        [Fact]
        public void Photo_with_same_name_and_size_but_other_content_gets_its_own_file()
        {
            var root = Path.Combine(Path.GetTempPath(), "xamlnav-" + Guid.NewGuid().ToString("N"));
            var first = Path.Combine(root, "a", "cat.png");
            var second = Path.Combine(root, "b", "cat.png");
            Directory.CreateDirectory(Path.GetDirectoryName(first));
            Directory.CreateDirectory(Path.GetDirectoryName(second));
            File.WriteAllBytes(first, new byte[] { 1, 2, 3 });
            File.WriteAllBytes(second, new byte[] { 3, 2, 1 });
            try
            {
                PhotoStore.FolderPath = Path.Combine(root, PhotoStore.FolderName);
                string error;

                Assert.Equal("cat.png", PhotoStore.Save(first, out error));
                Assert.Equal("cat_2.png", PhotoStore.Save(second, out error));
            }
            finally
            {
                PhotoStore.FolderPath = null;
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void Computed_values_are_converted_to_the_property_type()
        {
            var fields = new List<FieldDefinitionModel>
            {
                new FieldDefinitionModel { DisplayName = "Стоимость", PropertyName = "Cost", FieldType = FieldType.Computed,
                    Computed = new ComputedFieldModel { Terms = { new ComputedTerm { Property = "Weight" }, new ComputedTerm { Operation = ComputedOperation.Divide, Constant = 4 } } } }
            };
            var evaluator = new ComputedFieldEvaluator(typeof(Shipment), fields);
            var shipment = new Shipment { Weight = 10 };

            evaluator.Apply(shipment);

            Assert.Equal(2.5m, shipment.Cost);
            Assert.Empty(evaluator.Problems);
        }

        [Fact]
        public void Translator_replaces_library_strings()
        {
            var previous = NavigatorLocalization.Translator;
            try
            {
                NavigatorLocalization.Translator = s => s == "No records yet" ? "Пусто" : null;
                Assert.Equal("Пусто", NavigatorLocalization.Translate("No records yet"));
                Assert.Equal("Да", NavigatorLocalization.Translate("Yes"));
            }
            finally { NavigatorLocalization.Translator = previous; }
        }

        [Fact]
        public void Page_record_values_follow_json_changes()
        {
            var record = new PageRecordModel { ValuesJson = "{\"a\":1}" };
            Assert.Equal(1L, record["a"]);

            record.ValuesJson = "{\"a\":2}";
            Assert.Equal(2L, record["a"]);
            Assert.Null(record["missing"]);
        }

        [Fact]
        public void Circular_computed_fields_stay_reported_after_aggregates()
        {
            var fields = new List<FieldDefinitionModel>
            {
                new FieldDefinitionModel { DisplayName = "A", PropertyName = "Total", FieldType = FieldType.Computed,
                    Computed = new ComputedFieldModel { Terms = { new ComputedTerm { Property = "OrdersCount" } } } },
                new FieldDefinitionModel { DisplayName = "B", PropertyName = "OrdersCount", FieldType = FieldType.Computed,
                    Computed = new ComputedFieldModel { Terms = { new ComputedTerm { Property = "Total" } } } }
            };
            var evaluator = new ComputedFieldEvaluator(typeof(Customer), fields);
            Assert.Equal(2, evaluator.Problems.Count);

            evaluator.PrepareAggregates((table, type) => new List<object>());

            Assert.Equal(2, evaluator.Problems.Count);
            Assert.Contains("по кругу", evaluator.Problems[0]);
        }

        [Fact]
        public void Range_and_division_by_zero()
        {
            var fields = new List<FieldDefinitionModel>
            {
                new FieldDefinitionModel { DisplayName = "Скидка", PropertyName = "Total", FieldType = FieldType.Computed,
                    Computed = new ComputedFieldModel
                    {
                        Kind = ComputedKind.Range, RangeSourceProperty = "Quantity", Otherwise = 0,
                        Ranges = { new ComputedRange { To = 10, Value = 0 }, new ComputedRange { From = 10, To = 50, Value = 5 }, new ComputedRange { From = 50, Value = 10 } }
                    } },
                new FieldDefinitionModel { DisplayName = "Доля", PropertyName = "OrdersCount", FieldType = FieldType.Computed,
                    Computed = new ComputedFieldModel { Terms = { new ComputedTerm { Property = "Quantity" }, new ComputedTerm { Operation = ComputedOperation.Divide, Constant = 0 } } } }
            };
            var evaluator = new ComputedFieldEvaluator(typeof(Customer), fields);
            var customer = new Customer { Quantity = 25 };

            evaluator.Apply(customer);

            Assert.Equal(5d, customer.Total);
            Assert.Null(customer.OrdersCount);
            Assert.Contains(evaluator.Problems, p => p.Contains("деление на ноль"));
        }

        [Theory]
        [InlineData(TextRuleKind.Email, "name@mail.ru", true)]
        [InlineData(TextRuleKind.Email, "name@", false)]
        [InlineData(TextRuleKind.Phone, "+7 999 123-45-67", true)]
        [InlineData(TextRuleKind.LettersOnly, "Анна-Мария", true)]
        [InlineData(TextRuleKind.Digits, "12a", false)]
        public void Text_rules(TextRuleKind rule, string value, bool ok)
        {
            string error;
            Assert.Equal(ok, new FieldValidationModel { TextRule = rule }.IsSatisfiedBy(value, FieldType.Text, out error));
        }

        [Fact]
        public void Coercion_understands_spaces_and_commas()
        {
            object value;
            string error;
            Assert.True(DynamicPageViewModel.TryCoerce("1\u00A0234,5", typeof(decimal), out value, out error));
            Assert.Equal(1234.5m, value);
            Assert.True(DynamicPageViewModel.TryCoerce("", typeof(int?), out value, out error));
            Assert.Null(value);
            Assert.False(DynamicPageViewModel.TryCoerce("99999999999", typeof(int), out value, out error));
            Assert.Contains("Int32", error);
        }

        [Fact]
        public void Photo_saved_twice_reuses_the_same_file()
        {
            var root = Path.Combine(Path.GetTempPath(), "xamlnav-" + Guid.NewGuid().ToString("N"));
            var source = Path.Combine(root, "src", "cat.png");
            Directory.CreateDirectory(Path.GetDirectoryName(source));
            File.WriteAllBytes(source, new byte[] { 1, 2, 3 });
            try
            {
                PhotoStore.FolderPath = Path.Combine(root, PhotoStore.FolderName);
                string error;

                Assert.Equal("cat.png", PhotoStore.Save(source, out error));
                Assert.Equal("cat.png", PhotoStore.Save(source, out error));
                Assert.Equal("", error);
                Assert.NotNull(PhotoStore.Resolve("cat.jpg"));
            }
            finally
            {
                PhotoStore.FolderPath = null;
                Directory.Delete(root, true);
            }
        }
    }
}
