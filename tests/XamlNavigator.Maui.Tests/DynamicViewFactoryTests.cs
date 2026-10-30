using XamlNavigator.Abstractions;
using XamlNavigator.DynamicPages;
using XamlNavigator.DynamicPages.FieldViewModels;
using XamlNavigator.Models;
using XamlNavigator.Platform;
using Xunit;

namespace XamlNavigator.Maui.Tests
{
    public class Category
    {
        public long Id { get; set; }
        public string Title { get; set; }
    }

    public class Product
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Notes { get; set; }
        public decimal Price { get; set; }
        public int Count { get; set; }
        public double? Total { get; set; }
        public DateTime? Made { get; set; }
        public bool Active { get; set; }
        public string Kind { get; set; }
        public long? CategoryId { get; set; }
        public Category Category { get; set; }
        public string Photo { get; set; }
    }

    /// <summary>Таблицы в памяти: «Products» и «Categories».</summary>
    internal sealed class MemoryRepository : IDynamicPageRepository
    {
        public readonly Dictionary<string, List<object>> Tables = new()
        {
            ["Categories"] = [new Category { Id = 1, Title = "Хлеб" }, new Category { Id = 2, Title = "Молоко" }],
            ["Products"] =
            [
                new Product { Id = 1, Name = "Батон", Price = 45.5m, Count = 2, CategoryId = 1, Active = true, Made = new DateTime(2026, 10, 1) },
                new Product { Id = 2, Name = "Кефир", Price = 90m, Count = 3, CategoryId = 2 }
            ]
        };

        public Task<List<object>> GetRowsAsync(string tableName, Type modelType) =>
            Task.FromResult(Tables.TryGetValue(tableName, out var rows) ? rows.ToList() : new List<object>());

        public Task<long> InsertRowAsync(string tableName, Type modelType, object item)
        {
            var rows = Tables[tableName];
            var id = rows.Count + 1;
            modelType.GetProperty("Id")!.SetValue(item, (long)id);
            rows.Add(item);
            return Task.FromResult((long)id);
        }

        public Task<bool> DeleteRowAsync(string tableName, long id) =>
            Task.FromResult(Tables[tableName].RemoveAll(r => (long)r.GetType().GetProperty("Id")!.GetValue(r)! == id) > 0);

        public Type ResolveModelType(string typeName) => typeof(Product).Assembly.GetType(typeName);

        public Task<PageDefinitionModel> GetPageAsync(long pageId) => Task.FromResult(new PageDefinitionModel { Id = pageId });
        public Task<List<PageDefinitionModel>> GetPagesAsync() => Task.FromResult(new List<PageDefinitionModel>());
        public Task<long> InsertPageAsync(PageDefinitionModel page) => Task.FromResult(0L);
        public Task UpdatePageAsync(PageDefinitionModel page) => Task.CompletedTask;
        public Task DeletePageAsync(long pageId) => Task.CompletedTask;
        public Task EnsureTableAsync(string tableName, Type modelType) => Task.CompletedTask;
        public Task UpdateRowAsync(string tableName, Type modelType, object item) => Task.CompletedTask;
        public Task<List<FieldDefinitionModel>> GetFieldsAsync(long pageId) => Task.FromResult(new List<FieldDefinitionModel>());
        public Task SaveFieldsAsync(long pageId, List<FieldDefinitionModel> defs) => Task.CompletedTask;
        public Task DropTableAsync(string tableName) => Task.CompletedTask;
    }

    /// <summary>Оркестратор-пустышка: странице после сохранения нужно «назад».</summary>
    internal sealed class FakeNavigator : INavigationOrchestrator
    {
        public int BackCount { get; private set; }
        public Task<bool> OpenAsync<TViewModel>() where TViewModel : Core.BaseShellViewModel => Task.FromResult(true);
        public object CreateRootView<TViewModel>() where TViewModel : Core.BaseShellViewModel => null;
        public Task NavigateToAsync<TViewModel>() where TViewModel : Core.BaseContentViewModel => Task.CompletedTask;
        public Task NavigateToAsync<TViewModel, TParameter>(TParameter parameter) where TViewModel : Core.BaseContentViewModel => Task.CompletedTask;
        public Task NavigateToDynamicAsync(long pageId) => Task.CompletedTask;
        public Task NavigateToDynamicAsync(long pageId, object parameter) => Task.CompletedTask;
        public Task NavigateBackAsync()
        {
            BackCount++;
            return Task.CompletedTask;
        }
        public bool CanNavigateBack => false;
        public Core.BaseShellViewModel CurrentShell => null;
        public event EventHandler<NavigationErrorEventArgs> NavigationFailed { add { } remove { } }
        public event EventHandler HistoryChanged { add { } remove { } }
    }

    public class DynamicViewFactoryTests
    {
        public DynamicViewFactoryTests()
        {
            // Тесты проверяют русские тексты и формат чисел — не зависим от языка системы.
            var ru = new System.Globalization.CultureInfo("ru-RU");
            System.Threading.Thread.CurrentThread.CurrentCulture = ru;
            System.Threading.Thread.CurrentThread.CurrentUICulture = ru;
            MauiPlatform.Install();
        }

        private static List<FieldDefinitionModel> Fields() =>
        [
            new() { DisplayName = "Название", PropertyName = nameof(Product.Name), FieldType = FieldType.Text, IsRequired = true, SortOrder = 0,
                    Validation = new FieldValidationModel { Min = 2 } },
            new() { DisplayName = "Заметки", PropertyName = nameof(Product.Notes), FieldType = FieldType.MultiLine, SortOrder = 1, ShowInList = false },
            new() { DisplayName = "Цена", PropertyName = nameof(Product.Price), FieldType = FieldType.Number, SortOrder = 2 },
            new() { DisplayName = "Кол-во", PropertyName = nameof(Product.Count), FieldType = FieldType.Number, SortOrder = 3 },
            new() { DisplayName = "Сумма", PropertyName = nameof(Product.Total), FieldType = FieldType.Computed, SortOrder = 4,
                    Computed = new ComputedFieldModel { Decimals = 2, Suffix = "₽", Terms =
                    [
                        new ComputedTerm { Property = nameof(Product.Price) },
                        new ComputedTerm { Operation = ComputedOperation.Multiply, Property = nameof(Product.Count) }
                    ] } },
            new() { DisplayName = "Сделан", PropertyName = nameof(Product.Made), FieldType = FieldType.Date, SortOrder = 5 },
            new() { DisplayName = "В продаже", PropertyName = nameof(Product.Active), FieldType = FieldType.Bool, SortOrder = 6 },
            new() { DisplayName = "Вид", PropertyName = nameof(Product.Kind), FieldType = FieldType.Choice, SortOrder = 7, OptionsJson = "[\"Штучный\",\"Весовой\"]" },
            new() { DisplayName = "Категория", PropertyName = nameof(Product.CategoryId), FieldType = FieldType.Reference, SortOrder = 8,
                    ReferenceTable = "Categories", ReferenceModelTypeName = typeof(Category).FullName, ReferenceTitleProperty = nameof(Category.Title) },
            new() { DisplayName = "Фото", PropertyName = nameof(Product.Photo), FieldType = FieldType.Photo, SortOrder = 9 }
        ];

        private static (DynamicPageViewModel Vm, ContentView View) Open(MemoryRepository repository, PageDisplayMode mode, ListViewMode listMode = ListViewMode.Table)
        {
            var page = new PageDefinitionModel
            {
                Id = 10, PairedPageId = 11, Title = "Товары", TableName = "Products", DisplayMode = mode, ListViewMode = listMode,
                Highlight = new HighlightRuleModel { PropertyName = nameof(Product.Price), Comparison = HighlightComparison.Greater, Value = "50", Background = "#FFCC00" }
            };
            var factory = new DynamicViewFactory();
            factory.Register(page.Id, () => new DynamicPageViewModel(repository, new FakeNavigator(), page, Fields(), typeof(Product), null, null), 0);

            var pair = factory.CreateView(page.Id);
            var vm = Assert.IsType<DynamicPageViewModel>(pair.ViewModel);
            var view = Assert.IsType<ContentView>(pair.View);
            Assert.Same(vm, view.BindingContext);
            return (vm, view);
        }

        private static IEnumerable<T> Descendants<T>(Element root) where T : Element
        {
            foreach (var child in ((IElementController)root).LogicalChildren)
            {
                if (child is T match) yield return match;
                foreach (var nested in Descendants<T>(child)) yield return nested;
            }
        }

        [Theory]
        [InlineData(ListViewMode.Table)]
        [InlineData(ListViewMode.Cards)]
        [InlineData(ListViewMode.Custom)]
        public async Task List_page_builds_and_loads_records(ListViewMode listMode)
        {
            var repository = new MemoryRepository();
            var (vm, view) = Open(repository, PageDisplayMode.List, listMode);
            await vm.OnAppearingAsync();

            Assert.Equal(2, vm.RecordsView!.Cast<object>().Count());
            Assert.Equal("Хлеб", vm.DisplayValue(vm.Records[0], vm.FieldDefinitions.Single(f => f.FieldType == FieldType.Reference)));
            Assert.Equal(91.0, ((Product)vm.Records[0]).Total);   // 45,5 × 2

            // Шаблон строки собирается без платформы: привязки, конвертеры, подсветка, свайп.
            var list = Assert.Single(Descendants<CollectionView>(view));
            var row = list.ItemTemplate.CreateContent() as View;
            Assert.NotNull(row);
            row!.BindingContext = vm.Records[0];

            vm.SearchText = "кеф";
            Assert.Single(vm.RecordsView!.Cast<object>());
            Assert.Equal("Найдено: 1 из 2", vm.RecordsSummary);
        }

        [Fact]
        public async Task Table_row_shows_lookup_and_highlight()
        {
            var repository = new MemoryRepository();
            var (vm, view) = Open(repository, PageDisplayMode.List);
            await vm.OnAppearingAsync();

            var list = Descendants<CollectionView>(view).Single();
            var row = (View)list.ItemTemplate.CreateContent();
            row.BindingContext = vm.Records[1];   // Кефир, 90 > 50 — подсвечен

            var labels = Descendants<Label>(row).Select(l => l.Text).ToList();
            Assert.Contains("Молоко", labels);
            Assert.Contains("Кефир", labels);

            var grid = Descendants<Grid>(row).First();
            Assert.Equal(Microsoft.Maui.Graphics.Color.FromArgb("#FFCC00"), grid.BackgroundColor);
        }

        [Fact]
        public async Task Edit_page_builds_form_and_saves_through_bindings()
        {
            var repository = new MemoryRepository();
            var (vm, view) = Open(repository, PageDisplayMode.Edit);
            await vm.OnAppearingAsync();

            Assert.Equal(vm.Fields.Count, Descendants<View>(view).Count(v => v.BindingContext is FieldViewModel && v.Parent?.BindingContext is DynamicPageViewModel));

            var name = Descendants<Entry>(view).First(e => e.BindingContext is TextFieldViewModel);
            name.Text = "Сушки";
            Assert.Equal("Сушки", ((TextFieldViewModel)name.BindingContext).Value);

            var reference = Descendants<Picker>(view).Single(p => p.BindingContext is ReferenceFieldViewModel);
            Assert.Equal(2, reference.Items.Count);   // справочник загружен при появлении
            reference.SelectedIndex = 1;

            Descendants<Entry>(view).First(e => (e.BindingContext as FieldViewModel)?.PropertyName == nameof(Product.Price)).Text = "12,5";
            Descendants<Entry>(view).First(e => (e.BindingContext as FieldViewModel)?.PropertyName == nameof(Product.Count)).Text = "4";

            var computed = (ComputedFieldViewModel)vm.Fields.Single(f => f.FieldType == FieldType.Computed);
            Assert.Equal(50.0, computed.Value);   // пересчёт прямо по форме

            await vm.SaveRecordCommand.ExecuteAsync();
            Assert.True(string.IsNullOrEmpty(vm.ErrorMessage), vm.ErrorMessage);
            var saved = (Product)repository.Tables["Products"].Last();
            Assert.Equal("Сушки", saved.Name);
            Assert.Equal(12.5m, saved.Price);
            Assert.Equal(2L, saved.CategoryId);
        }

        [Fact]
        public async Task Edit_page_reports_validation_errors()
        {
            var repository = new MemoryRepository();
            var (vm, view) = Open(repository, PageDisplayMode.Edit);
            await vm.OnAppearingAsync();

            ((TextFieldViewModel)vm.Fields[0]).Value = "Х";
            await vm.SaveRecordCommand.ExecuteAsync();

            Assert.Contains("не короче 2 символов", vm.ErrorMessage);
            var error = Descendants<Label>(view).First(l => l.TextColor == Microsoft.Maui.Graphics.Colors.Red);
            Assert.True(error.IsVisible);
            Assert.Equal(2, repository.Tables["Products"].Count);
        }
    }
}
