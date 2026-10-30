using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XamlNavigator.Abstractions;
using XamlNavigator.DynamicPages;
using XamlNavigator.DynamicPages.FieldViewModels;
using XamlNavigator.Models;
using XamlNavigator.Services;
using XamlNavigator.Tests.Nav.ViewModels;
using Xunit;

namespace XamlNavigator.Tests
{
    public class Customer
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public double? Total { get; set; }
        public double? OrdersCount { get; set; }
        public DateTime? Since { get; set; }
        public bool Vip { get; set; }
    }

    public class Order
    {
        public long Id { get; set; }
        public long CustomerId { get; set; }
        public decimal Amount { get; set; }
    }

    /// <summary>Два внешних ключа на одну таблицу.</summary>
    public class Shipment
    {
        public long Id { get; set; }
        public long? SenderId { get; set; }
        public Customer Sender { get; set; }
        public long? ReceiverId { get; set; }
        public Customer Receiver { get; set; }
        public decimal Weight { get; set; }
        public decimal? Cost { get; set; }
    }

    public class DynamicPageTests
    {
        private const long ListPageId = 1;
        private const long EditPageId = 2;

        private readonly MemoryRepository _repository = new MemoryRepository();
        private readonly NavigationOrchestrator _nav;
        private readonly IDynamicViewFactory _factory;
        private readonly HomeViewModel _home;

        public DynamicPageTests()
        {
            TestPlatform.Install();
            _factory = new FakeDynamicViewFactory();
            _nav = (NavigationOrchestrator)NavigatorBuilder.Create(new Provider().Add(new Dependency()))
                .ScanAssembly(typeof(DynamicPageTests).Assembly)
                .UseDynamicViewFactory(_factory)
                .Build();
            _home = (HomeViewModel)((FakeView)_nav.CreateRootView<HomeViewModel>()).BindingContext;

            _repository.Table("Customers").AddRange(new object[]
            {
                new Customer { Id = 1, Name = "Иванов", Price = 1500m, Quantity = 2, Vip = true },
                new Customer { Id = 2, Name = "Петров", Price = 10m, Quantity = 3 }
            });
            _repository.Table("Orders").AddRange(new object[]
            {
                new Order { Id = 1, CustomerId = 1, Amount = 100 },
                new Order { Id = 2, CustomerId = 1, Amount = 50 },
                new Order { Id = 3, CustomerId = 2, Amount = 7 }
            });
        }

        private static List<FieldDefinitionModel> Fields() => new List<FieldDefinitionModel>
        {
            new FieldDefinitionModel { DisplayName = "Имя", PropertyName = "Name", FieldType = FieldType.Text, IsRequired = true, SortOrder = 0,
                Validation = new FieldValidationModel { Min = 2 } },
            new FieldDefinitionModel { DisplayName = "Цена", PropertyName = "Price", FieldType = FieldType.Number, SortOrder = 1 },
            new FieldDefinitionModel { DisplayName = "Количество", PropertyName = "Quantity", FieldType = FieldType.Number, SortOrder = 2,
                Validation = new FieldValidationModel { Min = 1, Max = 100 } },
            new FieldDefinitionModel { DisplayName = "Сумма", PropertyName = "Total", FieldType = FieldType.Computed, SortOrder = 3,
                Computed = new ComputedFieldModel
                {
                    Kind = ComputedKind.Arithmetic, Decimals = 2,
                    Terms = { new ComputedTerm { Property = "Price" }, new ComputedTerm { Operation = ComputedOperation.Multiply, Property = "Quantity" } }
                } },
            new FieldDefinitionModel { DisplayName = "Заказов", PropertyName = "OrdersCount", FieldType = FieldType.Computed, SortOrder = 4,
                Computed = new ComputedFieldModel
                {
                    Kind = ComputedKind.Aggregate, Function = AggregateFunction.Count, SourceTable = "Orders",
                    SourceModelTypeName = typeof(Order).FullName, SourceForeignKey = "CustomerId"
                } },
            new FieldDefinitionModel { DisplayName = "VIP", PropertyName = "Vip", FieldType = FieldType.Bool, SortOrder = 5 }
        };

        private DynamicPageViewModel Page(long id, PageDisplayMode mode, long paired)
        {
            var page = new PageDefinitionModel { Id = id, PairedPageId = paired, Title = mode == PageDisplayMode.List ? "Клиенты" : "Клиент", DisplayMode = mode, TableName = "Customers" };
            _repository.AddPage(page);
            return new DynamicPageViewModel(_repository, _nav, page, Fields(), typeof(Customer), null, null);
        }

        private DynamicPageViewModel RegisterPages(out DynamicPageViewModel edit)
        {
            var list = Page(ListPageId, PageDisplayMode.List, EditPageId);
            var editPage = Page(EditPageId, PageDisplayMode.Edit, ListPageId);
            _factory.Register(ListPageId, () => list, 0);
            _factory.Register(EditPageId, () => editPage, 0);
            edit = editPage;
            return list;
        }

        [Fact]
        public async Task List_loads_records_with_computed_fields_and_filters_by_search()
        {
            DynamicPageViewModel edit;
            var list = RegisterPages(out edit);

            await _nav.NavigateToDynamicAsync(ListPageId);

            Assert.Same(list, _home.CurrentView.ViewModel);
            Assert.Equal(2, list.Records.Count);
            var ivanov = (Customer)list.Records[0];
            Assert.Equal(3000d, ivanov.Total);
            Assert.Equal(2d, ivanov.OrdersCount);
            Assert.Equal("", list.ErrorMessage);
            Assert.Equal("Записей: 2", list.RecordsSummary);

            list.SearchText = "петр";
            Assert.Single(list.VisibleRecords());
            Assert.Equal("Найдено: 1 из 2", list.RecordsSummary);

            // «1 500» в списке находится и по «1500».
            list.SearchField = list.SearchFields.Single(f => f.Title == "Цена");
            list.SearchText = "1500";
            Assert.Single(list.VisibleRecords());

            list.SearchText = "";
            Assert.Equal(2, list.RecordsView.Cast<object>().Count());
        }

        [Fact]
        public async Task Save_validates_coerces_and_returns_back()
        {
            DynamicPageViewModel edit;
            var list = RegisterPages(out edit);
            await _nav.NavigateToDynamicAsync(ListPageId);

            list.AddRecordCommand.Execute(null);
            await Task.Delay(50);
            Assert.Same(edit, _home.CurrentView.ViewModel);

            Field<TextFieldViewModel>(edit, "Name").Value = "Я";
            Field<NumberFieldViewModel>(edit, "Price").Value = "1 234,5";
            Field<NumberFieldViewModel>(edit, "Quantity").Value = "2,5";
            await edit.SaveRecordCommand.ExecuteAsync();
            Assert.Contains("«Имя» — ожидается не короче 2 символов", edit.ErrorMessage);
            Assert.Contains("«Количество» — ожидается целое число, без дробной части", edit.ErrorMessage);

            Field<TextFieldViewModel>(edit, "Name").Value = "Сидоров";
            Field<NumberFieldViewModel>(edit, "Quantity").Value = "2";
            Assert.Equal(2469d, Field<ComputedFieldViewModel>(edit, "Total").Value);

            await edit.SaveRecordCommand.ExecuteAsync();

            Assert.Equal("", edit.ErrorMessage);
            var saved = (Customer)_repository.Table("Customers").Last();
            Assert.Equal("Сидоров", saved.Name);
            Assert.Equal(1234.5m, saved.Price);
            Assert.Same(list, _home.CurrentView.ViewModel);
            Assert.Equal(3, list.Records.Count);
        }

        [Fact]
        public async Task Required_fields_are_listed()
        {
            DynamicPageViewModel edit;
            RegisterPages(out edit);
            await _nav.NavigateToDynamicAsync(EditPageId);

            await edit.SaveRecordCommand.ExecuteAsync();

            Assert.Equal("Заполните обязательные поля: Имя", edit.ErrorMessage);
        }

        [Fact]
        public async Task Edit_existing_record_and_delete()
        {
            DynamicPageViewModel edit;
            var list = RegisterPages(out edit);
            await _nav.NavigateToDynamicAsync(ListPageId);

            list.EditRecordCommand.Execute(list.Records[1]);
            await Task.Delay(50);
            Assert.Same(edit, _home.CurrentView.ViewModel);
            Assert.Equal(2, edit.EditingId);
            Assert.Equal("Петров", Field<TextFieldViewModel>(edit, "Name").Value);

            Field<TextFieldViewModel>(edit, "Name").Value = "Петрова";
            await edit.SaveRecordCommand.ExecuteAsync();
            Assert.Equal("Петрова", ((Customer)_repository.Table("Customers")[1]).Name);
            Assert.Same(list, _home.CurrentView.ViewModel);

            await list.DeleteRecordCommand.ExecuteAsync(list.Records[0]);
            Assert.Single(list.Records);
        }

        [Fact]
        public async Task Report_contains_visible_rows_without_photos()
        {
            DynamicPageViewModel edit;
            var list = RegisterPages(out edit);
            await _nav.NavigateToDynamicAsync(ListPageId);
            list.SearchText = "Иванов";

            var report = list.BuildReport();

            Assert.Equal("Клиенты", report.Title);
            Assert.Contains("Иванов", report.Subtitle);
            Assert.Single(report.Rows);
            Assert.Equal("Да", report.Rows[0][5]);
            Assert.True(report.Columns[1].Numeric);
        }

        [Fact]
        public async Task Dynamic_page_respects_access_level()
        {
            var policy = new LevelPolicy { Level = 0 };
            var nav = (NavigationOrchestrator)NavigatorBuilder.Create(new Provider().Add(new Dependency()))
                .ScanAssembly(typeof(DynamicPageTests).Assembly)
                .UseDynamicViewFactory(_factory)
                .UseAccessPolicy(policy)
                .Build();
            var home = (HomeViewModel)((FakeView)nav.CreateRootView<HomeViewModel>()).BindingContext;
            _factory.Register(5, () => Page(5, PageDisplayMode.List, 0), 3);
            string failure = null;
            nav.NavigationFailed += (s, e) => failure = e.Message;

            await nav.NavigateToDynamicAsync(5);

            Assert.IsType<FirstViewModel>(home.CurrentView.ViewModel);
            Assert.NotNull(failure);
        }

        [Fact]
        public async Task Unregistered_dynamic_page_reports_failure()
        {
            string failure = null;
            _nav.NavigationFailed += (s, e) => failure = e.Message;

            await _nav.NavigateToDynamicAsync(404);

            Assert.Contains("404", failure);
            Assert.False(_nav.CanNavigateBack);
        }

        [Fact]
        public async Task Saving_an_edit_keeps_properties_that_are_not_on_the_form()
        {
            DynamicPageViewModel edit;
            var list = RegisterPages(out edit);
            var since = new DateTime(2020, 5, 1);
            ((Customer)_repository.Table("Customers")[1]).Since = since;
            await _nav.NavigateToDynamicAsync(ListPageId);

            list.EditRecordCommand.Execute(list.Records[1]);
            await Task.Delay(50);
            Field<TextFieldViewModel>(edit, "Name").Value = "Петрова";
            await edit.SaveRecordCommand.ExecuteAsync();

            var saved = (Customer)_repository.Table("Customers")[1];
            Assert.Equal("Петрова", saved.Name);
            Assert.Equal(since, saved.Since);
        }

        [Fact]
        public async Task Returning_to_a_new_record_form_keeps_what_was_typed()
        {
            DynamicPageViewModel edit;
            RegisterPages(out edit);
            await _nav.NavigateToDynamicAsync(EditPageId);
            Field<TextFieldViewModel>(edit, "Name").Value = "Черновик";

            await _nav.NavigateToAsync<SecondViewModel>();
            await _nav.NavigateBackAsync();
            Assert.Same(edit, _home.CurrentView.ViewModel);
            Assert.Equal("Черновик", Field<TextFieldViewModel>(edit, "Name").Value);

            // А «Добавить» (новая запись) форму очищает.
            await _nav.NavigateToDynamicAsync(EditPageId);
            Assert.Equal("", Field<TextFieldViewModel>(edit, "Name").Value);
        }

        private DynamicPageViewModel ShipmentPage(PageDisplayMode mode)
        {
            var page = new PageDefinitionModel { Id = 30, Title = "Отправки", DisplayMode = mode, TableName = "Shipments" };
            var fields = new List<FieldDefinitionModel>
            {
                new FieldDefinitionModel { DisplayName = "Отправитель", PropertyName = "SenderId", FieldType = FieldType.Reference, SortOrder = 0,
                    ReferenceTable = "Customers", ReferenceModelTypeName = typeof(Customer).FullName, ReferenceTitleProperty = "Name" },
                new FieldDefinitionModel { DisplayName = "Получатель", PropertyName = "ReceiverId", FieldType = FieldType.Reference, SortOrder = 1,
                    ReferenceTable = "Customers", ReferenceModelTypeName = typeof(Customer).FullName, ReferenceTitleProperty = "Name" },
                new FieldDefinitionModel { DisplayName = "Вес", PropertyName = "Weight", FieldType = FieldType.Number, SortOrder = 2 },
                new FieldDefinitionModel { DisplayName = "Стоимость", PropertyName = "Cost", FieldType = FieldType.Computed, SortOrder = 3,
                    Computed = new ComputedFieldModel { Decimals = 2, Terms = { new ComputedTerm { Property = "Weight" }, new ComputedTerm { Operation = ComputedOperation.Multiply, Constant = 2.5 } } } }
            };
            return new DynamicPageViewModel(_repository, _nav, page, fields, typeof(Shipment), null, null);
        }

        [Fact]
        public async Task Two_references_to_one_table_fill_their_own_navigation_properties_and_decimal_computed()
        {
            _repository.Table("Shipments").Add(new Shipment { Id = 1, SenderId = 1, ReceiverId = 2, Weight = 4 });
            var list = ShipmentPage(PageDisplayMode.List);
            _factory.Register(30, () => list, 0);

            await _nav.NavigateToDynamicAsync(30);

            var shipment = (Shipment)list.Records.Single();
            Assert.Equal("Иванов", shipment.Sender.Name);
            Assert.Equal("Петров", shipment.Receiver.Name);
            Assert.Equal(10m, shipment.Cost);
            Assert.Equal("", list.ErrorMessage);
        }

        [Fact]
        public async Task References_are_reloaded_on_each_appearance_and_unknown_keys_survive()
        {
            var form = ShipmentPage(PageDisplayMode.Edit);
            _factory.Register(31, () => form, 0);
            await _nav.NavigateToDynamicAsync(31);
            var sender = Field<ReferenceFieldViewModel>(form, "SenderId");
            Assert.Equal(2, sender.Items.Count);

            sender.SetValue(999L);                                  // записи нет в справочнике
            Assert.Equal(999L, sender.GetValue());

            _repository.Table("Customers").Add(new Customer { Id = 3, Name = "Сидоров" });
            await _nav.NavigateToAsync<SecondViewModel>();
            await _nav.NavigateBackAsync();

            Assert.Contains(sender.Items, i => i.Display == "Сидоров");
            Assert.Equal(999L, sender.GetValue());
            sender.SetValue(3L);
            Assert.Equal("Сидоров", sender.Selected.Display);
        }

        [Fact]
        public void Required_reference_with_key_zero_counts_as_empty()
        {
            var form = ShipmentPage(PageDisplayMode.Edit);
            var sender = Field<ReferenceFieldViewModel>(form, "SenderId");

            sender.SetValue(0L);

            Assert.Null(sender.GetValue());
        }

        [Fact]
        public void Empty_computed_result_resets_a_non_nullable_property()
        {
            var fields = new List<FieldDefinitionModel>
            {
                new FieldDefinitionModel { DisplayName = "Вес", PropertyName = "Weight", FieldType = FieldType.Computed,
                    Computed = new ComputedFieldModel { Terms = { new ComputedTerm { Constant = 1 }, new ComputedTerm { Operation = ComputedOperation.Divide, Constant = 0 } } } }
            };
            var evaluator = new ComputedFieldEvaluator(typeof(Shipment), fields);
            var shipment = new Shipment { Weight = 42 };

            evaluator.Apply(shipment);

            Assert.Equal(0m, shipment.Weight);
            Assert.Contains(evaluator.Problems, p => p.Contains("не посчитано"));
        }

        [Theory]
        [InlineData("1,5", 1.6, true)]
        [InlineData("1,5", 1.4, false)]
        [InlineData("1.5", 1.6, true)]
        [InlineData("1 500", 1600.0, true)]
        [InlineData("1 500", 1400.0, false)]
        public void Highlight_threshold_reads_russian_numbers(string threshold, double value, bool expected)
        {
            var rule = new HighlightRuleModel { PropertyName = "Price", Comparison = HighlightComparison.Greater, Value = threshold, Background = "#FFE0E0" };
            Assert.Equal(expected, rule.Matches(value));
        }

        private static T Field<T>(DynamicPageViewModel page, string property) where T : FieldViewModel =>
            (T)page.Fields.Single(f => f.PropertyName == property);
    }
}
