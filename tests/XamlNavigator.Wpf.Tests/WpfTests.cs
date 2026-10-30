using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XamlNavigator.Abstractions;
using XamlNavigator.Attributes;
using XamlNavigator.Core;
using XamlNavigator.DynamicPages;
using XamlNavigator.Models;
using XamlNavigator.Platform;
using XamlNavigator.Services;
using XamlNavigator.Wpf.Tests.ViewModels;
using Xunit;

namespace XamlNavigator.Wpf.Tests.ViewModels
{
    public class ShellViewModel : BaseWindowViewModel
    {
        public ShellViewModel() : base(typeof(HomeViewModel)) { }
    }

    [ChildOf(typeof(ShellViewModel), DisplayName = "Главная")]
    public class HomeViewModel : BaseUserControlViewModel { }

    public class ConfirmDialogViewModel : BaseDialogViewModel { }
}

namespace XamlNavigator.Wpf.Tests.Views
{
    public class ShellView : Window { }
    public class HomeView : UserControl { }
    public class ConfirmDialogView : UserControl { }
}

namespace XamlNavigator.Wpf.Tests
{
    public class Product
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Notes { get; set; }
        public decimal Price { get; set; }
        public DateTime? Since { get; set; }
        public bool Active { get; set; }
        public string Kind { get; set; }
        public long? CategoryId { get; set; }
        public double? Total { get; set; }
        public string Photo { get; set; }
    }

    public class Category
    {
        public long Id { get; set; }
        public string Title { get; set; }
    }

    /// <summary>Своё представление записи: свойство Caption привязывается к полю сущности.</summary>
    public class ProductCard : ContentControl
    {
        public static readonly DependencyProperty CaptionProperty =
            DependencyProperty.Register(nameof(Caption), typeof(string), typeof(ProductCard), new PropertyMetadata(null, (d, e) => ((ProductCard)d).Content = e.NewValue));

        public string Caption
        {
            get { return (string)GetValue(CaptionProperty); }
            set { SetValue(CaptionProperty, value); }
        }
    }

    internal sealed class Repository : IDynamicPageRepository
    {
        public readonly List<object> Products = new List<object>
        {
            new Product { Id = 1, Name = "Чайник", Price = 1500, Active = true, Kind = "Б", CategoryId = 1, Since = new DateTime(2026, 1, 2) },
            new Product { Id = 2, Name = "Кружка", Price = 200, Kind = "А", CategoryId = 2 }
        };

        public Task<List<PageDefinitionModel>> GetPagesAsync() => Task.FromResult(new List<PageDefinitionModel>());
        public Task<PageDefinitionModel> GetPageAsync(long pageId) => Task.FromResult<PageDefinitionModel>(null);
        public Task<long> InsertPageAsync(PageDefinitionModel page) => Task.FromResult(0L);
        public Task UpdatePageAsync(PageDefinitionModel page) => Task.CompletedTask;
        public Task DeletePageAsync(long pageId) => Task.CompletedTask;
        public Task EnsureTableAsync(string tableName, Type modelType) => Task.CompletedTask;

        public Task<List<object>> GetRowsAsync(string tableName, Type modelType) => Task.FromResult(tableName == "Categories"
            ? new List<object> { new Category { Id = 1, Title = "Посуда" }, new Category { Id = 2, Title = "Кухня" } }
            : Products.ToList());

        public Task<long> InsertRowAsync(string tableName, Type modelType, object item) => Task.FromResult(1L);
        public Task UpdateRowAsync(string tableName, Type modelType, object item) => Task.CompletedTask;
        public Task<bool> DeleteRowAsync(string tableName, long id) => Task.FromResult(true);
        public Task<List<FieldDefinitionModel>> GetFieldsAsync(long pageId) => Task.FromResult(new List<FieldDefinitionModel>());
        public Task SaveFieldsAsync(long pageId, List<FieldDefinitionModel> defs) => Task.CompletedTask;
        public Task DropTableAsync(string tableName) => Task.CompletedTask;
        public Type ResolveModelType(string typeName) => typeof(Repository).Assembly.GetType(typeName);
    }

    internal sealed class Provider : INavigationServiceProvider
    {
        public object GetService(Type serviceType) => null;
        public T GetService<T>() where T : class => null;
    }

    public class WpfTests
    {
        /// <summary>WPF требует STA; тесты xUnit идут в MTA.</summary>
        private static void Sta(Func<Task> body)
        {
            ExceptionDispatchInfo failure = null;
            var thread = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
                // Тесты проверяют русские тексты и формат чисел — не зависим от языка системы.
                var ru = new System.Globalization.CultureInfo("ru-RU");
                Thread.CurrentThread.CurrentCulture = ru;
                Thread.CurrentThread.CurrentUICulture = ru;
                var frame = new System.Windows.Threading.DispatcherFrame();
                Task run;
                try { run = body(); }
                catch (Exception ex)
                {
                    failure = ExceptionDispatchInfo.Capture(ex);
                    return;
                }
                run.ContinueWith(t =>
                {
                    if (t.Exception != null) failure = ExceptionDispatchInfo.Capture(t.Exception.InnerException);
                    frame.Continue = false;
                }, TaskScheduler.FromCurrentSynchronizationContext());
                System.Windows.Threading.Dispatcher.PushFrame(frame);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            failure?.Throw();
        }

        private static List<FieldDefinitionModel> Fields() => new List<FieldDefinitionModel>
        {
            new FieldDefinitionModel { DisplayName = "Название", PropertyName = "Name", FieldType = FieldType.Text, SortOrder = 0, Validation = new FieldValidationModel { Max = 50 } },
            new FieldDefinitionModel { DisplayName = "Заметки", PropertyName = "Notes", FieldType = FieldType.MultiLine, SortOrder = 1 },
            new FieldDefinitionModel { DisplayName = "Цена", PropertyName = "Price", FieldType = FieldType.Number, SortOrder = 2 },
            new FieldDefinitionModel { DisplayName = "С", PropertyName = "Since", FieldType = FieldType.Date, SortOrder = 3 },
            new FieldDefinitionModel { DisplayName = "Активен", PropertyName = "Active", FieldType = FieldType.Bool, SortOrder = 4 },
            new FieldDefinitionModel { DisplayName = "Вид", PropertyName = "Kind", FieldType = FieldType.Choice, OptionsJson = "[\"А\",\"Б\"]", SortOrder = 5 },
            new FieldDefinitionModel { DisplayName = "Категория", PropertyName = "CategoryId", FieldType = FieldType.Reference, SortOrder = 6,
                ReferenceTable = "Categories", ReferenceModelTypeName = typeof(Category).FullName, ReferenceTitleProperty = "Title" },
            new FieldDefinitionModel { DisplayName = "Итого", PropertyName = "Total", FieldType = FieldType.Computed, SortOrder = 7,
                Computed = new ComputedFieldModel { Decimals = 2, Suffix = "₽", Terms = { new ComputedTerm { Property = "Price" }, new ComputedTerm { Operation = ComputedOperation.Multiply, Constant = 2 } } } },
            new FieldDefinitionModel { DisplayName = "Фото", PropertyName = "Photo", FieldType = FieldType.Photo, SortOrder = 8 }
        };

        private static DynamicPageViewModel Page(PageDisplayMode mode, ListViewMode listMode, INavigationOrchestrator nav)
        {
            var page = new PageDefinitionModel
            {
                Id = 1, PairedPageId = 2, Title = "Товары", DisplayMode = mode, ListViewMode = listMode, TableName = "Products",
                Highlight = new HighlightRuleModel { PropertyName = "Price", Comparison = HighlightComparison.Greater, Value = "1000", Background = "#FFE0E0", Foreground = "#B00000" }
            };
            CustomViewInfo custom = null;
            List<CustomViewBindingDto> bindings = null;
            if (listMode == ListViewMode.Custom)
            {
                custom = new CustomViewInfo(typeof(ProductCard), "Карточка товара",
                    new List<CustomViewProperty> { new CustomViewProperty("Caption", "Подпись", typeof(string), ProductCard.CaptionProperty) });
                bindings = new List<CustomViewBindingDto> { new CustomViewBindingDto { ControlProperty = "Caption", ModelProperty = "Name", Header = "Товар: " } };
            }
            return new DynamicPageViewModel(new Repository(), nav, page, Fields(), typeof(Product), custom, bindings);
        }

        private static T Layout<T>(T element) where T : FrameworkElement
        {
            element.Measure(new Size(1000, 800));
            element.Arrange(new Rect(0, 0, 1000, 800));
            element.UpdateLayout();
            return element;
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }

        private static INavigationOrchestrator Navigator(IDynamicViewFactory factory = null) =>
            NavigatorBuilder.Create(new Provider()).ScanAssembly(typeof(WpfTests).Assembly).UseDynamicViewFactory(factory ?? new DynamicViewFactory()).Build();

        [Fact]
        public void Window_and_user_control_are_bound_by_name() => Sta(() =>
        {
            var nav = Navigator();
            var window = Assert.IsType<Views.ShellView>(nav.CreateRootView<ShellViewModel>());
            var shell = Assert.IsType<ShellViewModel>(window.DataContext);

            Assert.Equal("WPF", NavigatorPlatform.Name);
            Assert.IsType<Views.HomeView>(shell.CurrentView.View);
            Assert.IsType<HomeViewModel>(((FrameworkElement)shell.CurrentView.View).DataContext);
            Assert.Equal("Главная", shell.NavigationClusters.Single().Items.Single().DisplayName);
            window.Close();
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(PageDisplayMode.Edit, ListViewMode.Table)]
        [InlineData(PageDisplayMode.List, ListViewMode.Table)]
        [InlineData(PageDisplayMode.List, ListViewMode.Cards)]
        [InlineData(PageDisplayMode.List, ListViewMode.Custom)]
        public void Dynamic_page_views_are_built(PageDisplayMode mode, ListViewMode listMode) => Sta(async () =>
        {
            var factory = new DynamicViewFactory();
            var nav = Navigator(factory);
            nav.CreateRootView<ShellViewModel>();
            var page = Page(mode, listMode, nav);
            factory.Register(1, () => page, 0);

            await nav.NavigateToDynamicAsync(1);
            var shell = (ShellViewModel)nav.CurrentShell;
            Assert.Same(page, shell.CurrentView.ViewModel);

            var view = Layout(Assert.IsType<UserControl>(shell.CurrentView.View));
            Assert.Same(page, view.DataContext);
            var tree = Descendants(view).ToList();

            if (mode == PageDisplayMode.Edit)
            {
                Assert.True(tree.OfType<TextBox>().Count() >= 3, "поля формы не построились");
                Assert.Single(tree.OfType<DatePicker>());
                Assert.Single(tree.OfType<CheckBox>());
                Assert.Equal(2, tree.OfType<ComboBox>().Count());
                Assert.Contains(tree.OfType<TextBlock>(), t => t.Text == "не длиннее 50 символов");
                Assert.Contains(tree.OfType<Button>(), b => (b.Content as string) == "Выбрать фото…");
                return;
            }

            Assert.Equal(2, page.Records.Count);
            Assert.Contains(tree.OfType<Button>(), b => (b.Content as string) == "Печать");
            if (listMode == ListViewMode.Table)
            {
                var grid = tree.OfType<DataGrid>().Single();
                Assert.Equal(2, grid.Items.Count);
                Assert.NotNull(grid.RowStyle);
                Assert.Contains(tree.OfType<TextBlock>(), t => t.Text == "Посуда");
                Assert.Contains(tree.OfType<TextBlock>(), t => t.Text == "3 000,00 ₽" || t.Text == "3,000.00 ₽" || t.Text.Replace("\u00A0", " ").Replace("\u202F", " ") == "3 000,00 ₽");
            }
            else if (listMode == ListViewMode.Cards)
            {
                Assert.Contains(tree.OfType<TextBlock>(), t => t.Text == "Чайник");
                Assert.Contains(tree.OfType<Border>(), b => (b.Background as SolidColorBrush)?.Color == (Color)ColorConverter.ConvertFromString("#FFE0E0"));
                // Цвет текста правила — только у подсвеченной карточки; у остальных остаётся обычный.
                var red = (Color)ColorConverter.ConvertFromString("#B00000");
                Assert.Equal(red, ((SolidColorBrush)tree.OfType<TextBlock>().First(t => t.Text == "Чайник").Foreground).Color);
                Assert.NotEqual(red, ((SolidColorBrush)tree.OfType<TextBlock>().First(t => t.Text == "Кружка").Foreground).Color);
            }
            else
            {
                Assert.Contains(tree.OfType<ProductCard>(), c => c.Caption == "Товар: Чайник");
            }

            page.SearchText = "круж";
            Assert.Single(page.VisibleRecords());
        });

        [Fact]
        public void Edit_form_speaks_english_under_english_culture() => Sta(async () =>
        {
            var en = new System.Globalization.CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = en;
            Thread.CurrentThread.CurrentUICulture = en;
            var factory = new DynamicViewFactory();
            var nav = Navigator(factory);
            nav.CreateRootView<ShellViewModel>();
            factory.Register(1, () => Page(PageDisplayMode.Edit, ListViewMode.Table, nav), 0);

            await nav.NavigateToDynamicAsync(1);

            var view = Layout((UserControl)nav.CurrentShell.CurrentView.View);
            var tree = Descendants(view).ToList();
            var buttons = tree.OfType<Button>().Select(b => b.Content as string).ToList();
            Assert.Contains("Save", buttons);
            Assert.Contains("Choose photo…", buttons);
            Assert.Contains(tree.OfType<TextBlock>(), t => t.Text == "at most 50 characters");
            Assert.Contains(tree.OfType<TextBlock>(), t => t.Text == "calculated automatically, no need to enter it");
        });

        [Fact]
        public void Empty_shell_shows_placeholder() => Sta(() =>
        {
            WpfPlatform.Install();
            var view = (UserControl)NavigatorPlatform.ViewAdapter.CreateEmptyContentView(new EmptyContentViewModel("Пусто", "Нет экранов"));
            Layout(view);
            Assert.Contains(Descendants(view).OfType<TextBlock>(), t => t.Text == "Нет экранов");
            return Task.CompletedTask;
        });

        [Fact]
        public void Dialog_host_has_default_template_and_report_builds() => Sta(() =>
        {
            WpfPlatform.Install();
            var host = new DialogHostWindow { Content = new TextBlock { Text = "x" } };
            Assert.True(host.ApplyTemplate());
            Assert.NotNull(host.Template.FindName("PART_DragArea", host));

            var document = ReportBuilder.Build(new Report("Товары", null,
                new List<ReportColumn> { new ReportColumn("Название", false), new ReportColumn("Цена", true) },
                new List<IList<string>> { new List<string> { "Чайник", "1500" } }));
            Assert.Equal(4, document.Blocks.Count);
            return Task.CompletedTask;
        });
    }
}
