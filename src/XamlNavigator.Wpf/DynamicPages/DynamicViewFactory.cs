using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Security;
using XamlNavigator.Localization;
using XamlNavigator.Core;
using XamlNavigator.DynamicPages.FieldViewModels;
using XamlNavigator.Models;
using XamlNavigator.Platform;

namespace XamlNavigator.DynamicPages
{
    /// <summary>
    /// WPF-представления динамических страниц, построенные в коде: форма записи или список
    /// (DataGrid, карточки, своё представление) с поиском, печатью и подсветкой. Цвета — из ресурсов
    /// приложения AccentColor, SecondaryColor, SurfaceAltColor, если они есть.
    /// </summary>
    public class DynamicViewFactory : DynamicViewFactoryBase
    {
        static DynamicViewFactory() => WpfPlatform.Install();

        protected override object BuildView(BaseContentViewModel viewModel) => BuildUserControl(viewModel);

        private static UserControl BuildUserControl(BaseContentViewModel vm)
        {
            var dynamicVm = vm as DynamicPageViewModel;
            if (dynamicVm == null) return Wrap(BuildEdit());
            switch (dynamicVm.DisplayMode)
            {
                case PageDisplayMode.List: return Wrap(BuildList(dynamicVm));
                default: return Wrap(BuildEdit());
            }
        }

        private static UserControl Wrap(UIElement content) => new UserControl() { Content = content };

        private static UIElement BuildList(DynamicPageViewModel d)
        {
            UIElement content;
            switch (d.ListViewMode)
            {
                case ListViewMode.Cards: content = BuildCards(d); break;
                case ListViewMode.Custom: content = BuildCustom(d); break;
                default: content = BuildTable(d); break;
            }

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var search = BuildSearchPanel(d);
            Grid.SetRow(search, 0);
            root.Children.Add(search);

            Grid.SetRow(content, 1);
            root.Children.Add(content);

            var summary = BuildSummary();
            Grid.SetRow(summary, 2);
            root.Children.Add(summary);

            var error = BuildErrorText();
            Grid.SetRow(error, 3);
            root.Children.Add(error);

            if (d.PairedPageId != 0)
            {
                var actions = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                actions.Children.Add(BuildActionButton(Tr.T("Add"), nameof(DynamicPageViewModel.AddRecordCommand)));
                Grid.SetRow(actions, 4);
                root.Children.Add(actions);
            }

            return root;
        }

        private static TextBlock BuildErrorText()
        {
            var error = new TextBlock
            {
                Foreground = Brushes.Red,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 4)
            };
            error.SetBinding(TextBlock.TextProperty, new Binding(nameof(BaseContentViewModel.ErrorMessage)));
            return error;
        }

        private static UIElement BuildEdit()
        {
            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var error = BuildErrorText();
            Grid.SetRow(error, 0);
            root.Children.Add(error);

            var fieldsControl = new ItemsControl();
            foreach (var kv in FieldTemplates) fieldsControl.Resources.Add(new DataTemplateKey(kv.Key), kv.Value);
            fieldsControl.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(DynamicPageViewModel.Fields)));

            var scroller = new ScrollViewer
            {
                Content = fieldsControl,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Grid.SetRow(scroller, 1);
            root.Children.Add(scroller);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            actions.Children.Add(BuildActionButton(Tr.T("Back"), nameof(DynamicPageViewModel.GoBackCommand)));
            actions.Children.Add(BuildActionButton(Tr.T("Clear"), nameof(DynamicPageViewModel.ClearFormCommand)));
            actions.Children.Add(BuildActionButton(Tr.T("Delete"), nameof(DynamicPageViewModel.DeleteCurrentCommand)));
            actions.Children.Add(BuildActionButton(Tr.T("Save"), nameof(DynamicPageViewModel.SaveRecordCommand)));
            Grid.SetRow(actions, 2);
            root.Children.Add(actions);

            return root;
        }

        private static UIElement BuildSearchPanel(DynamicPageViewModel d)
        {
            var panel = new Grid { Margin = new Thickness(10, 10, 10, 6) };
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var caption = new TextBlock
            {
                Text = Tr.T("Search"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            caption.SetResourceReference(TextBlock.ForegroundProperty, "AccentColor");
            Grid.SetColumn(caption, 0);
            panel.Children.Add(caption);

            var box = new TextBox { Height = 32, VerticalContentAlignment = VerticalAlignment.Center };
            box.SetBinding(TextBox.TextProperty, new Binding(nameof(DynamicPageViewModel.SearchText))
            {
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            Grid.SetColumn(box, 1);
            panel.Children.Add(box);

            var where = new ComboBox
            {
                Height = 32,
                MinWidth = 150,
                Margin = new Thickness(10, 0, 0, 0),
                ToolTip = Tr.T("Search in")
            };
            where.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(DynamicPageViewModel.SearchFields)));
            where.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(DynamicPageViewModel.SearchField)));
            Grid.SetColumn(where, 2);
            panel.Children.Add(where);

            var clear = new Button
            {
                Content = Tr.T("Reset"),
                Height = 32,
                MinWidth = 100,
                Margin = new Thickness(10, 0, 0, 0)
            };
            clear.SetBinding(ButtonBase.CommandProperty, new Binding(nameof(DynamicPageViewModel.ClearSearchCommand)));
            Grid.SetColumn(clear, 3);
            panel.Children.Add(clear);

            var print = new Button
            {
                Content = Tr.T("Print"),
                Height = 32,
                MinWidth = 100,
                Margin = new Thickness(10, 0, 0, 0),
                ToolTip = Tr.T("Prints what is currently in the list. To get a PDF file, choose the \"Microsoft Print to PDF\" printer.")
            };
            print.SetBinding(ButtonBase.CommandProperty, new Binding(nameof(DynamicPageViewModel.PrintCommand)));
            Grid.SetColumn(print, 4);
            if (d.CanPrint) panel.Children.Add(print);

            return panel;
        }

        private static TextBlock BuildSummary()
        {
            var summary = new TextBlock
            {
                Margin = new Thickness(14, 4, 14, 0),
                FontSize = 12,
                Opacity = 0.8
            };
            summary.SetResourceReference(TextBlock.ForegroundProperty, "AccentColor");
            summary.SetBinding(TextBlock.TextProperty, new Binding(nameof(DynamicPageViewModel.RecordsSummary)));
            return summary;
        }

        private static Button BuildActionButton(string content, string commandPath)
        {
            var b = new Button
            {
                Content = content,
                Width = 150,
                Height = 35,
                FontSize = 16,
                Margin = new Thickness(10, 10, 10, 30)
            };
            b.SetBinding(Button.CommandProperty, new Binding(commandPath));
            return b;
        }

        private static UIElement BuildTable(DynamicPageViewModel d)
        {
            // Правка и добавление — через парную страницу-форму; в самой таблице изменения никуда бы не сохранились.
            var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserDeleteRows = false };
            grid.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(DynamicPageViewModel.RecordsView)));

            var hasPhoto = false;
            var numericColumns = new List<DataGridColumn>();
            foreach (var f in d.ListFields)
            {
                if (f.FieldType == FieldType.Photo)
                {
                    hasPhoto = true;
                    grid.Columns.Add(BuildPhotoColumn(f));
                    continue;
                }

                // Привязки WPF форматируют по Language элемента (en-US), а поиск и печать — по текущей культуре.
                var binding = new Binding(f.PropertyName) { ConverterCulture = CultureInfo.CurrentCulture };
                if (f.FieldType == FieldType.Reference) binding.Converter = new ReferenceLookupConverter(d, f.PropertyName);
                else if (f.FieldType == FieldType.Computed) binding.Converter = new ComputedDisplayConverter(f.Computed);
                else if (f.FieldType == FieldType.Date) binding.StringFormat = DateFormat;
                else if (f.FieldType == FieldType.Bool) binding.Converter = new BoolDisplayConverter();

                var numeric = f.FieldType == FieldType.Number || f.FieldType == FieldType.Computed;
                var column = new DataGridTextColumn
                {
                    Header = f.DisplayName,
                    Binding = binding,
                    ElementStyle = CellTextStyle(numeric),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                    IsReadOnly = f.FieldType == FieldType.Computed
                };

                if (numeric)
                {
                    column.HeaderStyle = NumericHeaderStyle(grid);
                    numericColumns.Add(column);
                }

                grid.Columns.Add(column);
            }

            if (hasPhoto) grid.RowHeight = 56;

            HighlightPainter.Apply(grid, d.Highlight);

            // Стили строятся на основе стилей приложения. Пока таблица не в окне, видны только ресурсы Application;
            // в окне — и ресурсы окна/страницы, поэтому основу пересчитываем один раз после Loaded.
            RoutedEventHandler restyle = null;
            restyle = (s, e) =>
            {
                grid.Loaded -= restyle;
                foreach (var column in numericColumns) column.HeaderStyle = NumericHeaderStyle(grid);
                HighlightPainter.Apply(grid, d.Highlight);
            };
            grid.Loaded += restyle;

            AttachRowContextMenu(grid);
            grid.PreviewMouseRightButtonDown += SelectRowOnRightClick;
            grid.MouseDoubleClick += (s, e) =>
            {
                // Двойной щелчок по заголовку или полосе прокрутки запись не открывает.
                if (FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject) == null) return;
                var vm = grid.DataContext as DynamicPageViewModel;
                if (vm != null && grid.SelectedItem != null && vm.EditRecordCommand.CanExecute(grid.SelectedItem))
                    vm.EditRecordCommand.Execute(grid.SelectedItem);
            };

            return grid;
        }

        private static DataGridColumn BuildPhotoColumn(FieldDefinitionModel f)
        {
            var image = new FrameworkElementFactory(typeof(Image));
            image.SetBinding(Image.SourceProperty, new Binding(f.PropertyName)
            {
                Converter = new PhotoImageConverter(),
                ConverterParameter = PhotoThumbnailHeight
            });
            image.SetValue(FrameworkElement.HeightProperty, (double)PhotoThumbnailHeight);
            image.SetValue(Image.StretchProperty, Stretch.Uniform);
            image.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 4, 8, 4));
            image.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            image.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
            image.SetBinding(FrameworkElement.ToolTipProperty, new Binding(f.PropertyName));

            return new DataGridTemplateColumn
            {
                Header = f.DisplayName,
                CellTemplate = new DataTemplate { VisualTree = image },
                Width = new DataGridLength(90),
                CanUserSort = false,
                IsReadOnly = true
            };
        }

        private const string DateFormat = "dd.MM.yyyy";

        private const int PhotoThumbnailHeight = 44;
        private const int PhotoCardHeight = 96;

        private static Style CellTextStyle(bool numeric)
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
            style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(14, 0, 10, 0)));
            style.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty,
                numeric ? HorizontalAlignment.Right : HorizontalAlignment.Left));
            return style;
        }

        private static Style NumericHeaderStyle(FrameworkElement scope)
        {
            // Основа — ColumnHeaderStyle из стиля таблицы приложения, иначе неявный стиль заголовка.
            var style = new Style(typeof(DataGridColumnHeader));
            style.BasedOn = (scope as DataGrid)?.ColumnHeaderStyle ?? HighlightPainter.FindStyle(scope, typeof(DataGridColumnHeader));

            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Right));
            return style;
        }

        private static void AttachRowContextMenu(DataGrid grid)
        {
            var menu = new ContextMenu();
            menu.Items.Add(BuildMenuItem(Tr.T("Edit"), nameof(DynamicPageViewModel.EditRecordCommand), grid));
            menu.Items.Add(BuildMenuItem(Tr.T("Delete"), nameof(DynamicPageViewModel.DeleteRecordCommand), grid));
            grid.ContextMenu = menu;
        }

        private static MenuItem BuildMenuItem(string header, string commandName, DataGrid grid)
        {
            var item = new MenuItem { Header = header };
            item.SetBinding(MenuItem.CommandProperty, new Binding($"DataContext.{commandName}") { Source = grid });
            item.SetBinding(MenuItem.CommandParameterProperty, new Binding(nameof(DataGrid.SelectedItem)) { Source = grid });
            return item;
        }

        private static void SelectRowOnRightClick(object sender, MouseButtonEventArgs e)
        {
            var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row != null) row.IsSelected = true;
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                var parent = child as T;
                if (parent != null) return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private static UIElement BuildCards(DynamicPageViewModel d)
        {
            var items = new ItemsControl { ItemTemplate = BuildCardTemplate(d) };
            items.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(DynamicPageViewModel.RecordsView)));

            var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackFactory.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            items.ItemsPanel = new ItemsPanelTemplate(stackFactory);

            var itemContainerStyle = new Style(typeof(ContentPresenter));
            itemContainerStyle.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
            items.ItemContainerStyle = itemContainerStyle;

            return new ScrollViewer
            {
                Content = items,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
        }

        private static DataTemplate BuildCardTemplate(DynamicPageViewModel d)
        {
            var editCommand = d.EditRecordCommand;
            var deleteCommand = d.DeleteRecordCommand;

            var content = new FrameworkElementFactory(typeof(StackPanel));
            var textStyle = HighlightPainter.TextStyleFor(d.Highlight);

            foreach (var f in d.ListFields)
            {
                var row = new FrameworkElementFactory(typeof(StackPanel));
                row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                row.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 2));

                if (f.FieldType == FieldType.Photo)
                {
                    var photo = new FrameworkElementFactory(typeof(Image));
                    photo.SetBinding(Image.SourceProperty, new Binding(f.PropertyName)
                    {
                        Converter = new PhotoImageConverter(),
                        ConverterParameter = PhotoCardHeight
                    });
                    photo.SetValue(FrameworkElement.HeightProperty, (double)PhotoCardHeight);
                    photo.SetValue(Image.StretchProperty, Stretch.Uniform);
                    photo.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                    photo.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 4, 0, 4));
                    photo.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
                    content.AppendChild(photo);
                    continue;
                }

                var caption = new FrameworkElementFactory(typeof(TextBlock));
                caption.SetValue(TextBlock.TextProperty, f.DisplayName + ": ");
                caption.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
                if (textStyle != null) caption.SetValue(FrameworkElement.StyleProperty, textStyle);
                row.AppendChild(caption);

                var binding = new Binding(f.PropertyName) { ConverterCulture = CultureInfo.CurrentCulture };
                if (f.FieldType == FieldType.Reference) binding.Converter = new ReferenceLookupConverter(d, f.PropertyName);
                else if (f.FieldType == FieldType.Computed) binding.Converter = new ComputedDisplayConverter(f.Computed);
                else if (f.FieldType == FieldType.Date) binding.StringFormat = DateFormat;
                else if (f.FieldType == FieldType.Bool) binding.Converter = new BoolDisplayConverter();

                var value = new FrameworkElementFactory(typeof(TextBlock));
                value.SetBinding(TextBlock.TextProperty, binding);
                value.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
                if (textStyle != null) value.SetValue(FrameworkElement.StyleProperty, textStyle);
                row.AppendChild(value);

                content.AppendChild(row);
            }

            var card = new FrameworkElementFactory(typeof(Border));
            card.SetResourceReference(Border.BorderBrushProperty, "AccentColor");
            card.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            card.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            card.SetValue(Border.PaddingProperty, new Thickness(10));
            var highlight = HighlightPainter.BrushFor(d.Highlight);
            if (highlight != null)
            {
                var paint = new Binding(d.Highlight.PropertyName)
                {
                    Converter = new CardHighlightConverter(d.Highlight, highlight)
                };
                card.SetBinding(Border.BackgroundProperty, paint);

            }
            else card.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            card.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 5, 10, 5));
            card.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            card.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);
            card.SetValue(UIElement.RenderTransformOriginProperty, new Point(0.5, 0.5));
            card.AppendChild(content);

            card.AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) =>
            {
                var fe = s as FrameworkElement;
                if (fe == null) return;
                if (!(fe.RenderTransform is ScaleTransform)) fe.RenderTransform = new ScaleTransform(1, 1);
                if (fe.ContextMenu == null) fe.ContextMenu = BuildCardContextMenu(fe, editCommand, deleteCommand);
            }));

            card.AddHandler(UIElement.MouseEnterEvent, new MouseEventHandler((s, e) => ScaleCard(s, 1.01)));
            card.AddHandler(UIElement.MouseLeaveEvent, new MouseEventHandler((s, e) => ScaleCard(s, 1)));

            card.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler((s, e) =>
            {
                var fe = s as FrameworkElement;
                var item = fe != null ? fe.DataContext : null;
                if (item != null && editCommand.CanExecute(item)) editCommand.Execute(item);
            }));

            return new DataTemplate { VisualTree = card };
        }

        private static ContextMenu BuildCardContextMenu(FrameworkElement owner, ICommand editCommand, ICommand deleteCommand)
        {
            var edit = new MenuItem { Header = Tr.T("Edit"), Command = editCommand };
            var remove = new MenuItem { Header = Tr.T("Delete"), Command = deleteCommand };

            var menu = new ContextMenu();
            menu.Items.Add(edit);
            menu.Items.Add(remove);

            menu.Opened += (s, e) =>
            {
                edit.CommandParameter = owner.DataContext;
                remove.CommandParameter = owner.DataContext;
            };

            return menu;
        }

        private static void ScaleCard(object sender, double to)
        {
            var fe = sender as FrameworkElement;
            var scale = fe != null ? fe.RenderTransform as ScaleTransform : null;
            if (scale == null) return;

            var animation = new DoubleAnimation(to, new Duration(TimeSpan.FromSeconds(0.15)));
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        private static UIElement BuildCustom(DynamicPageViewModel d)
        {
            var template = BuildCustomItemTemplate(d);
            var items = new ItemsControl { ItemTemplate = template };
            items.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(DynamicPageViewModel.RecordsView)));

            var stack = new FrameworkElementFactory(typeof(StackPanel));
            stack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            items.ItemsPanel = new ItemsPanelTemplate(stack);

            return new ScrollViewer
            {
                Content = items,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
        }

        private static DataTemplate BuildCustomItemTemplate(DynamicPageViewModel d)
        {
            var info = d.CustomViewInfo;
            if (info == null) return new DataTemplate();

            var rootFactory = new FrameworkElementFactory(info.ControlType);
            rootFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 5, 10, 5));

            foreach (var b in d.CustomBindings)
            {
                var fld = info.Properties.FirstOrDefault(x => x.PropertyName == b.ControlProperty);
                var dp = fld?.TargetProperty as DependencyProperty;
                if (dp == null || string.IsNullOrEmpty(b.ModelProperty)) continue;

                if (!string.IsNullOrEmpty(b.Header) && fld.PropertyType == typeof(string))
                    rootFactory.SetBinding(dp,
                        new Binding(b.ModelProperty) { StringFormat = EscapeFormat(b.Header) + "{0}" });
                else rootFactory.SetBinding(dp, new Binding(b.ModelProperty));
            }

            var hostFactory = new FrameworkElementFactory(typeof(Border));
            hostFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            hostFactory.SetValue(Border.CursorProperty, Cursors.Hand);
            hostFactory.AppendChild(rootFactory);

            hostFactory.AddHandler(UIElement.MouseLeftButtonDownEvent,
                new MouseButtonEventHandler((s, e) =>
                {
                    if (e.ClickCount != 2) return;
                    var fe = s as FrameworkElement;
                    if (fe == null) return;
                    var item = fe.DataContext;
                    if (item != null && d.EditRecordCommand.CanExecute(item))
                        d.EditRecordCommand.Execute(item);
                }));

            return new DataTemplate { VisualTree = hostFactory };
        }

        private static Dictionary<Type, DataTemplate> _fieldTemplates;
        private static Func<string, string> _templatesTranslator;
        private static string _templatesCulture;

        /// <summary>
        /// Шаблоны полей формы; пересобираются, если сменился переводчик строк или язык интерфейса
        /// (CurrentUICulture). Приложение, которое меняет язык иначе, вызывает <see cref="ResetFieldTemplates"/>.
        /// </summary>
        private static Dictionary<Type, DataTemplate> FieldTemplates
        {
            get
            {
                var culture = CultureInfo.CurrentUICulture.Name;
                if (_fieldTemplates == null || !ReferenceEquals(_templatesTranslator, NavigatorLocalization.Translator) || _templatesCulture != culture)
                {
                    _templatesTranslator = NavigatorLocalization.Translator;
                    _templatesCulture = culture;
                    _fieldTemplates = CreateFieldTemplates();
                }
                return _fieldTemplates;
            }
        }

        /// <summary>Пересобрать шаблоны полей формы при следующем открытии страницы — после смены языка.</summary>
        public static void ResetFieldTemplates() => _fieldTemplates = null;

        /// <summary>Переведённая строка, безопасная для вставки в XAML-атрибут.</summary>
        private static string Text(string russian)
        {
            var escaped = SecurityElement.Escape(Tr.T(russian)) ?? "";
            return escaped.StartsWith("{", StringComparison.Ordinal) ? "{}" + escaped : escaped;
        }

        /// <summary>Подпись из настроек страницы как префикс StringFormat: фигурные скобки в ней — текст, а не место значения.</summary>
        private static string EscapeFormat(string text) => text.Replace("{", "{{").Replace("}", "}}");

        private static Dictionary<Type, DataTemplate> CreateFieldTemplates()
        {
            const string ns =
                "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
                "xmlns:f=\"clr-namespace:XamlNavigator.DynamicPages.FieldViewModels;assembly=XamlNavigator\" " +
                "xmlns:c=\"clr-namespace:XamlNavigator.DynamicPages;assembly=XamlNavigator.Wpf\"";

            const string hint =
                @"<DataTemplate.Resources><c:EmptyToCollapsedConverter x:Key=""HintVisibility""/></DataTemplate.Resources>";
            const string hintBlock =
                @"<TextBlock Text=""{Binding Hint}"" FontSize=""11"" Opacity=""0.7"" Margin=""2,2,0,0""
                             Visibility=""{Binding Hint, Converter={StaticResource HintVisibility}}""/>";

            Func<string, DataTemplate> parseTemplate = xaml => (DataTemplate)XamlReader.Parse(xaml);

            return new Dictionary<Type, DataTemplate>
            {
                [typeof(TextFieldViewModel)] = parseTemplate($@"
                <DataTemplate {ns} DataType=""{{x:Type f:TextFieldViewModel}}"">
                  {hint}
                  <StackPanel Margin=""0,4"">
                    <TextBlock Text=""{{Binding DisplayName}}"" FontWeight=""Bold""/>
                    <TextBox Text=""{{Binding Value, UpdateSourceTrigger=PropertyChanged}}""/>
                    {hintBlock}
                  </StackPanel>
                </DataTemplate>"),

                [typeof(ComputedFieldViewModel)] = parseTemplate($@"
                <DataTemplate {ns} DataType=""{{x:Type f:ComputedFieldViewModel}}"">
                  <StackPanel Margin=""0,4"">
                    <TextBlock Text=""{{Binding DisplayName}}"" FontWeight=""Bold""/>
                    <Border BorderThickness=""1"" CornerRadius=""10"" Padding=""12,8"" Opacity=""0.85""
                            BorderBrush=""{{DynamicResource SecondaryColor}}""
                            Background=""{{DynamicResource SurfaceAltColor}}"">
                      <TextBlock Text=""{{Binding Display}}"" FontSize=""14""/>
                    </Border>
                    <TextBlock Text=""{Text("calculated automatically, no need to enter it")}"" FontSize=""11"" Opacity=""0.7"" Margin=""2,2,0,0""/>
                  </StackPanel>
                </DataTemplate>"),

                [typeof(NumberFieldViewModel)] = parseTemplate($@"
                <DataTemplate {ns} DataType=""{{x:Type f:NumberFieldViewModel}}"">
                  {hint}
                  <StackPanel Margin=""0,4"">
                    <TextBlock Text=""{{Binding DisplayName}}"" FontWeight=""Bold""/>
                    <TextBox Text=""{{Binding Value, UpdateSourceTrigger=PropertyChanged}}""/>
                    {hintBlock}
                  </StackPanel>
                </DataTemplate>"),

                [typeof(MultiLineFieldViewModel)] = parseTemplate($@"
                <DataTemplate {ns} DataType=""{{x:Type f:MultiLineFieldViewModel}}"">
                  {hint}
                  <StackPanel Margin=""0,4"">
                    <TextBlock Text=""{{Binding DisplayName}}"" FontWeight=""Bold""/>
                    <TextBox Text=""{{Binding Value, UpdateSourceTrigger=PropertyChanged}}""
                             AcceptsReturn=""True"" TextWrapping=""Wrap"" MinHeight=""60""
                             VerticalScrollBarVisibility=""Auto""/>
                    {hintBlock}
                  </StackPanel>
                </DataTemplate>"),

                [typeof(BoolFieldViewModel)] = parseTemplate($@"
                <DataTemplate {ns} DataType=""{{x:Type f:BoolFieldViewModel}}"">
                  <CheckBox Margin=""0,8"" Content=""{{Binding DisplayName}}"" IsChecked=""{{Binding Value}}""/>
                </DataTemplate>"),

                [typeof(DateFieldViewModel)] = parseTemplate($@"
                <DataTemplate {ns} DataType=""{{x:Type f:DateFieldViewModel}}"">
                  <StackPanel Margin=""0,4"">
                    <TextBlock Text=""{{Binding DisplayName}}"" FontWeight=""Bold""/>
                    <DatePicker SelectedDate=""{{Binding Value}}""/>
                  </StackPanel>
                </DataTemplate>"),

                [typeof(ChoiceFieldViewModel)] = parseTemplate($@"
                <DataTemplate {ns} DataType=""{{x:Type f:ChoiceFieldViewModel}}"">
                  <StackPanel Margin=""0,4"">
                    <TextBlock Text=""{{Binding DisplayName}}"" FontWeight=""Bold""/>
                    <ComboBox ItemsSource=""{{Binding Options}}"" SelectedItem=""{{Binding Selected}}""/>
                  </StackPanel>
                </DataTemplate>"),

                [typeof(ReferenceFieldViewModel)] = parseTemplate($@"
                <DataTemplate {ns} DataType=""{{x:Type f:ReferenceFieldViewModel}}"">
                  <StackPanel Margin=""0,4"">
                    <TextBlock Text=""{{Binding DisplayName}}"" FontWeight=""Bold""/>
                    <ComboBox ItemsSource=""{{Binding Items}}""
                              SelectedItem=""{{Binding Selected}}""
                              DisplayMemberPath=""Display""/>
                  </StackPanel>
                </DataTemplate>"),

                [typeof(PhotoFieldViewModel)] = parseTemplate($@"
                <DataTemplate {ns} DataType=""{{x:Type f:PhotoFieldViewModel}}"">
                  <DataTemplate.Resources>
                    <BooleanToVisibilityConverter x:Key=""PlaceholderVisibility""/>
                  </DataTemplate.Resources>
                  <StackPanel Margin=""0,4"">
                    <TextBlock Text=""{{Binding DisplayName}}"" FontWeight=""Bold""/>
                    <StackPanel Orientation=""Horizontal"" Margin=""0,4,0,0"">
                      <Border Width=""200"" Height=""200"" CornerRadius=""10"" BorderThickness=""1""
                              BorderBrush=""{{DynamicResource SecondaryColor}}""
                              Background=""{{DynamicResource SurfaceAltColor}}"">
                        <Grid>
                          <TextBlock Text=""{Text("No photo")}"" FontSize=""12"" Opacity=""0.6""
                                     HorizontalAlignment=""Center"" VerticalAlignment=""Center""
                                     Visibility=""{{Binding ShowPlaceholder, Converter={{StaticResource PlaceholderVisibility}}}}""/>
                          <Image Source=""{{Binding Preview}}"" Stretch=""Uniform"" Margin=""8""/>
                        </Grid>
                      </Border>
                      <StackPanel Margin=""14,0,0,0"" VerticalAlignment=""Center"">
                        <Button Content=""{Text("Choose photo…")}"" Command=""{{Binding PickCommand}}""
                                Width=""210"" Height=""34"" FontSize=""14"" Margin=""0,0,0,8""/>
                        <Button Content=""{Text("Remove photo")}"" Command=""{{Binding ClearCommand}}""
                                Width=""210"" Height=""34"" FontSize=""14"" Margin=""0,0,0,8""/>
                        <Button Content=""{Text("Open photo folder")}"" Command=""{{Binding OpenFolderCommand}}""
                                Width=""210"" Height=""34"" FontSize=""13""/>
                      </StackPanel>
                    </StackPanel>
                    <TextBlock Text=""{{Binding Status}}"" FontSize=""11"" Opacity=""0.7""
                               TextWrapping=""Wrap"" Margin=""2,6,0,0""/>
                  </StackPanel>
                </DataTemplate>"),
            };
        }
    }
}