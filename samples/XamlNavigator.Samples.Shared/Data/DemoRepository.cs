using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XamlNavigator.DynamicPages;
using XamlNavigator.Models;
using XamlNavigator.Samples.Models;

namespace XamlNavigator.Samples.Data
{
    /// <summary>
    /// Динамические страницы и их данные в памяти. В настоящем приложении всё это лежит в базе:
    /// описания страниц и полей — в служебных таблицах, записи — в таблицах сущностей.
    /// </summary>
    public class DemoRepository : IDynamicPageRepository
    {
        public const long CategoriesList = 1;
        public const long CategoryForm = 2;
        public const long ProductsTable = 3;
        public const long ProductForm = 4;
        public const long ProductsCards = 5;

        private readonly object _sync = new object();
        private readonly Dictionary<string, List<object>> _tables = new Dictionary<string, List<object>>();
        private readonly Dictionary<long, PageDefinitionModel> _pages = new Dictionary<long, PageDefinitionModel>();
        private readonly Dictionary<long, List<FieldDefinitionModel>> _fields = new Dictionary<long, List<FieldDefinitionModel>>();
        private long _nextId = 100;

        public DemoRepository()
        {
            Table("Categories").AddRange(new object[]
            {
                new Category { Id = 1, Title = "Kitchenware" },
                new Category { Id = 2, Title = "Home Textiles" },
                new Category { Id = 3, Title = "Appliances" }
            });
            Table("Products").AddRange(new object[]
            {
                new Product { Id = 1, Name = "Enamel Kettle", CategoryId = 1, Price = 1450, Quantity = 12, InStock = true, Unit = "pcs", ArrivedAt = DateTime.Today.AddDays(-3) },
                new Product { Id = 2, Name = "Plate Set", CategoryId = 1, Price = 2390, Quantity = 4, InStock = true, Unit = "set", ArrivedAt = DateTime.Today.AddDays(-20) },
                new Product { Id = 3, Name = "Waffle Towel", CategoryId = 2, Price = 180, Quantity = 60, InStock = true, Unit = "pcs" },
                new Product { Id = 4, Name = "Linen Tablecloth", CategoryId = 2, Price = 990, Quantity = 0, Unit = "pcs", Description = "Awaiting restock" },
                new Product { Id = 5, Name = "Hand Mixer", CategoryId = 3, Price = 3200, Quantity = 7, InStock = true, Unit = "pcs", ArrivedAt = DateTime.Today }
            });

            AddPage(new PageDefinitionModel { Id = CategoriesList, PairedPageId = CategoryForm, Title = "Categories", DisplayMode = PageDisplayMode.List, TableName = "Categories", ModelTypeName = typeof(Category).FullName },
                CategoryFields());
            AddPage(new PageDefinitionModel { Id = CategoryForm, PairedPageId = CategoriesList, Title = "Category", DisplayMode = PageDisplayMode.Edit, TableName = "Categories", ModelTypeName = typeof(Category).FullName },
                CategoryFields());
            AddPage(new PageDefinitionModel
            {
                Id = ProductsTable, PairedPageId = ProductForm, Title = "Products", DisplayMode = PageDisplayMode.List, ListViewMode = ListViewMode.Table,
                TableName = "Products", ModelTypeName = typeof(Product).FullName,
                Highlight = new HighlightRuleModel { PropertyName = "Quantity", Comparison = HighlightComparison.Equal, Value = "0", Background = "#FDE2E1" }
            }, ProductFields());
            AddPage(new PageDefinitionModel
            {
                Id = ProductsCards, PairedPageId = ProductForm, Title = "Showcase", DisplayMode = PageDisplayMode.List, ListViewMode = ListViewMode.Cards,
                TableName = "Products", ModelTypeName = typeof(Product).FullName,
                Highlight = new HighlightRuleModel { PropertyName = "Price", Comparison = HighlightComparison.GreaterOrEqual, Value = "2000", Background = "#E3F2FD" },
                Operations = PageOperations.Read | PageOperations.Update
            }, ProductFields());
            AddPage(new PageDefinitionModel { Id = ProductForm, PairedPageId = ProductsTable, Title = "Product", DisplayMode = PageDisplayMode.Edit, TableName = "Products", ModelTypeName = typeof(Product).FullName },
                ProductFields());
        }

        private static List<FieldDefinitionModel> CategoryFields() => new List<FieldDefinitionModel>
        {
            new FieldDefinitionModel { DisplayName = "Name", PropertyName = "Title", FieldType = FieldType.Text, IsRequired = true, SortOrder = 0,
                Validation = new FieldValidationModel { Min = 2, Max = 40 } },
            new FieldDefinitionModel { DisplayName = "Product count", PropertyName = "ProductsCount", FieldType = FieldType.Computed, SortOrder = 1,
                Computed = new ComputedFieldModel
                {
                    Kind = ComputedKind.Aggregate, Function = AggregateFunction.Count, SourceTable = "Products", SourceTitle = "Products",
                    SourceModelTypeName = typeof(Product).FullName, SourceForeignKey = "CategoryId"
                } }
        };

        private static List<FieldDefinitionModel> ProductFields() => new List<FieldDefinitionModel>
        {
            new FieldDefinitionModel { DisplayName = "Name", PropertyName = "Name", FieldType = FieldType.Text, IsRequired = true, SortOrder = 0,
                Validation = new FieldValidationModel { Min = 3, Max = 60 } },
            new FieldDefinitionModel { DisplayName = "Category", PropertyName = "CategoryId", FieldType = FieldType.Reference, IsRequired = true, SortOrder = 1,
                ReferenceTable = "Categories", ReferenceModelTypeName = typeof(Category).FullName, ReferenceTitleProperty = "Title" },
            new FieldDefinitionModel { DisplayName = "Price", PropertyName = "Price", FieldType = FieldType.Number, IsRequired = true, SortOrder = 2,
                Validation = new FieldValidationModel { Min = 0, Max = 1000000 } },
            new FieldDefinitionModel { DisplayName = "Quantity", PropertyName = "Quantity", FieldType = FieldType.Number, SortOrder = 3,
                Validation = new FieldValidationModel { Min = 0 } },
            new FieldDefinitionModel { DisplayName = "Total", PropertyName = "Total", FieldType = FieldType.Computed, SortOrder = 4,
                Computed = new ComputedFieldModel
                {
                    Kind = ComputedKind.Arithmetic, Decimals = 2, Suffix = "₽",
                    Terms = { new ComputedTerm { Property = "Price" }, new ComputedTerm { Operation = ComputedOperation.Multiply, Property = "Quantity" } }
                } },
            new FieldDefinitionModel { DisplayName = "Discount (%)", PropertyName = "Discount", FieldType = FieldType.Computed, SortOrder = 5, ShowInList = false,
                Computed = new ComputedFieldModel
                {
                    Kind = ComputedKind.Range, RangeSourceProperty = "Quantity", Otherwise = 0,
                    Ranges = { new ComputedRange { From = 10, To = 50, Value = 5 }, new ComputedRange { From = 50, Value = 10 } }
                } },
            new FieldDefinitionModel { DisplayName = "Unit", PropertyName = "Unit", FieldType = FieldType.Choice, OptionsJson = "[\\\"pcs\\\",\\\"set\\\",\\\"kg\\\",\\\"m\\\"]", SortOrder = 6 },
            new FieldDefinitionModel { DisplayName = "Arrived", PropertyName = "ArrivedAt", FieldType = FieldType.Date, SortOrder = 7 },
            new FieldDefinitionModel { DisplayName = "In stock", PropertyName = "InStock", FieldType = FieldType.Bool, SortOrder = 8 },
            new FieldDefinitionModel { DisplayName = "Description", PropertyName = "Description", FieldType = FieldType.MultiLine, SortOrder = 9, ShowInList = false },
            new FieldDefinitionModel { DisplayName = "Photo", PropertyName = "Photo", FieldType = FieldType.Photo, SortOrder = 10, ShowInList = false }
        };

        private void AddPage(PageDefinitionModel page, List<FieldDefinitionModel> fields)
        {
            foreach (var field in fields) field.PageId = page.Id;
            _pages[page.Id] = page;
            _fields[page.Id] = fields;
        }

        private List<object> Table(string name)
        {
            List<object> rows;
            if (!_tables.TryGetValue(name, out rows)) _tables[name] = rows = new List<object>();
            return rows;
        }

        private static long IdOf(object row) => (long)row.GetType().GetProperty("Id").GetValue(row);

        public Task<List<PageDefinitionModel>> GetPagesAsync()
        {
            lock (_sync) return Task.FromResult(_pages.Values.OrderBy(p => p.Id).ToList());
        }

        public Task<PageDefinitionModel> GetPageAsync(long pageId)
        {
            PageDefinitionModel page;
            lock (_sync) return Task.FromResult(_pages.TryGetValue(pageId, out page) ? page : null);
        }

        public Task<long> InsertPageAsync(PageDefinitionModel page)
        {
            lock (_sync)
            {
                page.Id = ++_nextId;
                _pages[page.Id] = page;
                return Task.FromResult(page.Id);
            }
        }

        public Task UpdatePageAsync(PageDefinitionModel page)
        {
            lock (_sync) _pages[page.Id] = page;
            return Task.CompletedTask;
        }

        public Task DeletePageAsync(long pageId)
        {
            lock (_sync) _pages.Remove(pageId);
            return Task.CompletedTask;
        }

        public Task EnsureTableAsync(string tableName, Type modelType)
        {
            lock (_sync) Table(tableName);
            return Task.CompletedTask;
        }

        public async Task<List<object>> GetRowsAsync(string tableName, Type modelType)
        {
            await Task.Delay(30).ConfigureAwait(false);
            lock (_sync) return Table(tableName).ToList();
        }

        public Task<long> InsertRowAsync(string tableName, Type modelType, object item)
        {
            lock (_sync)
            {
                var id = ++_nextId;
                modelType.GetProperty("Id").SetValue(item, id);
                Table(tableName).Add(item);
                return Task.FromResult(id);
            }
        }

        public Task UpdateRowAsync(string tableName, Type modelType, object item)
        {
            lock (_sync)
            {
                var rows = Table(tableName);
                var index = rows.FindIndex(r => IdOf(r) == IdOf(item));
                if (index >= 0) rows[index] = item;
            }
            return Task.CompletedTask;
        }

        public Task<bool> DeleteRowAsync(string tableName, long id)
        {
            lock (_sync)
            {
                // Как внешний ключ в базе: категорию с товарами удалить нельзя.
                if (tableName == "Categories" && Table("Products").OfType<Product>().Any(p => p.CategoryId == id)) return Task.FromResult(false);
                return Task.FromResult(Table(tableName).RemoveAll(r => IdOf(r) == id) > 0);
            }
        }

        public Task<List<FieldDefinitionModel>> GetFieldsAsync(long pageId)
        {
            List<FieldDefinitionModel> fields;
            lock (_sync) return Task.FromResult(_fields.TryGetValue(pageId, out fields) ? fields : new List<FieldDefinitionModel>());
        }

        public Task SaveFieldsAsync(long pageId, List<FieldDefinitionModel> defs)
        {
            lock (_sync) _fields[pageId] = defs;
            return Task.CompletedTask;
        }

        public Task DropTableAsync(string tableName)
        {
            lock (_sync) _tables.Remove(tableName);
            return Task.CompletedTask;
        }

        public Type ResolveModelType(string typeName) => typeof(DemoRepository).Assembly.GetType(typeName);
    }
}
