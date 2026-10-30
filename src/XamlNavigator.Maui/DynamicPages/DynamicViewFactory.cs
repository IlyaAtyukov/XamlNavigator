using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using XamlNavigator.Core;
using XamlNavigator.DynamicPages.FieldViewModels;
using XamlNavigator.Localization;
using XamlNavigator.Models;

namespace XamlNavigator.DynamicPages
{
    /// <summary>
    /// Представления динамических страниц для MAUI, построенные кодом: форма записи или список
    /// (таблица, карточки, своё представление) с поиском. Тап по записи — открыть её, свайп влево
    /// или правая кнопка мыши — удалить.
    /// </summary>
    public class DynamicViewFactory : DynamicViewFactoryBase
    {
        private const string DateFormat = "dd.MM.yyyy";
        private const int PhotoThumbnailHeight = 44;
        private const int PhotoCardHeight = 96;
        private const int PhotoPreviewSize = 200;
        private const int MinColumnWidth = 90;
        private const int PhotoColumnWidth = 90;
        private const int SearchPanelNarrowWidth = 560;

        protected override object BuildView(BaseContentViewModel viewModel)
        {
            var d = viewModel as DynamicPageViewModel;
            if (d == null) return new ContentView();
            return new ContentView { Content = d.DisplayMode == PageDisplayMode.List ? BuildList(d) : BuildEdit(d) };
        }

        #region Форма записи

        private static View BuildEdit(DynamicPageViewModel d)
        {
            var root = new Grid
            {
                Padding = new Thickness(16),
                RowDefinitions = Rows(GridLength.Auto, GridLength.Star, GridLength.Auto)
            };

            root.Add(BuildErrorLabel(), 0, 0);

            // Справа отступ: полоса прокрутки Windows рисуется поверх содержимого.
            var form = new VerticalStackLayout { Spacing = 6, Padding = new Thickness(0, 0, 16, 0) };
            foreach (var field in d.Fields) form.Add(BuildField(field));
            root.Add(new ScrollView { Content = form }, 0, 1);

            var actions = ActionsPanel();
            actions.Add(ActionButton(Tr.T("Back"), nameof(DynamicPageViewModel.GoBackCommand)));
            actions.Add(ActionButton(Tr.T("Clear"), nameof(DynamicPageViewModel.ClearFormCommand)));
            actions.Add(ActionButton(Tr.T("Delete"), nameof(DynamicPageViewModel.DeleteCurrentCommand)));
            actions.Add(ActionButton(Tr.T("Save"), nameof(DynamicPageViewModel.SaveRecordCommand)));
            root.Add(actions, 0, 2);

            return root;
        }

        private static View BuildField(FieldViewModel field)
        {
            View view;
            if (field is ComputedFieldViewModel) view = BuildComputedField();
            else if (field is PhotoFieldViewModel) view = BuildPhotoField((PhotoFieldViewModel)field);
            else if (field is BoolFieldViewModel) view = BuildBoolField();
            else if (field is NumberFieldViewModel) view = Labeled(field, Bound(new Entry { Keyboard = Keyboard.Numeric }, Entry.TextProperty, "Value"));
            else if (field is TextFieldViewModel) view = Labeled(field, Bound(new Entry(), Entry.TextProperty, "Value"));
            else if (field is MultiLineFieldViewModel)
                view = Labeled(field, Bound(new Editor { AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 60 }, Editor.TextProperty, "Value"));
            else if (field is DateFieldViewModel) view = Labeled(field, DateEditor());
            else if (field is ChoiceFieldViewModel)
                view = Labeled(field, Bound(new Picker { ItemsSource = ((ChoiceFieldViewModel)field).Options }, Picker.SelectedItemProperty, "Selected"));
            else if (field is ReferenceFieldViewModel)
            {
                var picker = new Picker { ItemDisplayBinding = new Binding(nameof(ReferenceListItem.Display)) };
                picker.SetBinding(Picker.ItemsSourceProperty, new Binding(nameof(ReferenceFieldViewModel.Items)));
                view = Labeled(field, Bound(picker, Picker.SelectedItemProperty, nameof(ReferenceFieldViewModel.Selected)));
            }
            else view = Labeled(field, new Label());

            view.BindingContext = field;
            return view;
        }

        private static View Labeled(FieldViewModel field, View input)
        {
            var stack = new VerticalStackLayout { Margin = new Thickness(0, 4) };
            stack.Add(Caption());
            stack.Add(input);
            if (!string.IsNullOrWhiteSpace(field.Hint))
                stack.Add(new Label { Text = field.Hint, FontSize = 11, Opacity = 0.7, Margin = new Thickness(2, 2, 0, 0) });
            return stack;
        }

        private static Label Caption()
        {
            var caption = new Label { FontAttributes = FontAttributes.Bold };
            caption.SetBinding(Label.TextProperty, new Binding(nameof(FieldViewModel.DisplayName)));
            return caption;
        }

        private static View BuildBoolField()
        {
            // У CheckBox в Windows нативная минимальная ширина 120 — без сброса подпись уезжает вправо.
            var check = Bound(new CheckBox { MinimumWidthRequest = 0 }, CheckBox.IsCheckedProperty, nameof(BoolFieldViewModel.Value));
            var caption = new Label { VerticalOptions = LayoutOptions.Center };
            caption.SetBinding(Label.TextProperty, new Binding(nameof(FieldViewModel.DisplayName)));
            return new HorizontalStackLayout { Margin = new Thickness(0, 8), Children = { check, caption } };
        }

        private static View BuildComputedField()
        {
            var value = new Label { FontSize = 14 };
            value.SetBinding(Label.TextProperty, new Binding(nameof(ComputedFieldViewModel.Display)));

            var stack = new VerticalStackLayout { Margin = new Thickness(0, 4) };
            stack.Add(Caption());
            stack.Add(new Border
            {
                StrokeThickness = 1,
                Stroke = Colors.Gray,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(12, 8),
                Opacity = 0.85,
                Content = value
            });
            stack.Add(new Label { Text = Tr.T("calculated automatically, no need to enter it"), FontSize = 11, Opacity = 0.7, Margin = new Thickness(2, 2, 0, 0) });
            return stack;
        }

        private static View BuildPhotoField(PhotoFieldViewModel photo)
        {
            var placeholder = new Label
            {
                Text = Tr.T("No photo"),
                FontSize = 12,
                Opacity = 0.6,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            placeholder.SetBinding(VisualElement.IsVisibleProperty, new Binding(nameof(PhotoFieldViewModel.ShowPlaceholder)));

            var image = new Image { Aspect = Aspect.AspectFit, Margin = new Thickness(8) };
            image.SetBinding(Image.SourceProperty, new Binding(nameof(PhotoFieldViewModel.Preview)));

            var preview = new Border
            {
                WidthRequest = PhotoPreviewSize,
                HeightRequest = PhotoPreviewSize,
                StrokeThickness = 1,
                Stroke = Colors.Gray,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Content = new Grid { Children = { placeholder, image } }
            };

            var buttons = new VerticalStackLayout { Spacing = 8, Margin = new Thickness(14, 0, 0, 0), VerticalOptions = LayoutOptions.Center };
            buttons.Add(CommandButton(Tr.T("Choose photo…"), nameof(PhotoFieldViewModel.PickCommand)));
            buttons.Add(CommandButton(Tr.T("Remove photo"), nameof(PhotoFieldViewModel.ClearCommand)));
            if (photo.CanOpenFolder) buttons.Add(CommandButton(Tr.T("Open photo folder"), nameof(PhotoFieldViewModel.OpenFolderCommand)));

            FlexLayout.SetShrink(preview, 0);
            FlexLayout.SetShrink(buttons, 0);

            var status = new Label { FontSize = 11, Opacity = 0.7, LineBreakMode = LineBreakMode.WordWrap, Margin = new Thickness(2, 6, 0, 0) };
            status.SetBinding(Label.TextProperty, new Binding(nameof(PhotoFieldViewModel.Status)));

            var stack = new VerticalStackLayout { Margin = new Thickness(0, 4) };
            stack.Add(Caption());
            stack.Add(new FlexLayout
            {
                Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
                AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center,
                Margin = new Thickness(0, 4, 0, 0),
                Children = { preview, buttons }
            });
            stack.Add(status);
            return stack;
        }

        #endregion

        #region Список записей

        private static View BuildList(DynamicPageViewModel d)
        {
            View content;
            switch (d.ListViewMode)
            {
                case ListViewMode.Cards: content = BuildCards(d); break;
                case ListViewMode.Custom: content = BuildCustom(d); break;
                default: content = BuildTable(d); break;
            }

            var root = new Grid { RowDefinitions = Rows(GridLength.Auto, GridLength.Star, GridLength.Auto, GridLength.Auto, GridLength.Auto) };
            root.Add(BuildSearchPanel(d), 0, 0);
            root.Add(content, 0, 1);
            root.Add(BuildSummary(), 0, 2);
            root.Add(BuildErrorLabel(), 0, 3);

            if (d.PairedPageId != 0)
            {
                var actions = ActionsPanel();
                actions.Add(ActionButton(Tr.T("Add"), nameof(DynamicPageViewModel.AddRecordCommand)));
                root.Add(actions, 0, 4);
            }

            return root;
        }

        private static View BuildSearchPanel(DynamicPageViewModel d)
        {
            var panel = new Grid
            {
                Margin = new Thickness(10, 10, 10, 6),
                ColumnSpacing = 8,
                RowSpacing = 6,
                RowDefinitions = Rows(GridLength.Auto, GridLength.Auto),
                ColumnDefinitions = Columns(GridLength.Star, GridLength.Auto, GridLength.Auto, GridLength.Auto)
            };

            var box = Bound(new Entry { Placeholder = Tr.T("Search"), VerticalOptions = LayoutOptions.Center }, Entry.TextProperty, nameof(DynamicPageViewModel.SearchText));
            panel.Add(box, 0, 0);


            var where = new Picker
            {
                Title = Tr.T("Search in"),
                WidthRequest = 170,
                VerticalOptions = LayoutOptions.Center,
                ItemsSource = d.SearchFields,
                ItemDisplayBinding = new Binding(nameof(SearchFieldOption.Title))
            };
            where.SetBinding(Picker.SelectedItemProperty, new Binding(nameof(DynamicPageViewModel.SearchField), BindingMode.TwoWay));
            panel.Add(where, 1, 0);

            panel.Add(CommandButton(Tr.T("Reset"), nameof(DynamicPageViewModel.ClearSearchCommand)), 2, 0);
            if (d.CanPrint) panel.Add(CommandButton(Tr.T("Print"), nameof(DynamicPageViewModel.PrintCommand)), 3, 0);

            // На узком экране поиск — отдельной строкой, под ним выбор поля и кнопки.
            panel.SizeChanged += (sender, e) =>
            {
                if (panel.Width <= 0) return;
                var narrow = panel.Width < SearchPanelNarrowWidth;
                Grid.SetColumnSpan(box, narrow ? 4 : 1);
                Grid.SetColumn(where, narrow ? 0 : 1);
                Grid.SetColumnSpan(where, narrow ? 2 : 1);
                where.WidthRequest = narrow ? -1 : 170;
                foreach (var child in panel.Children.OfType<View>().Where(c => !ReferenceEquals(c, box)))
                    Grid.SetRow(child, narrow ? 1 : 0);
            };

            return panel;
        }

        private static Label BuildSummary()
        {
            var summary = new Label { Margin = new Thickness(14, 4, 14, 0), FontSize = 12, Opacity = 0.8 };
            summary.SetBinding(Label.TextProperty, new Binding(nameof(DynamicPageViewModel.RecordsSummary)));
            return summary;
        }

        private static CollectionView RecordsList(DataTemplate template)
        {
            var list = new CollectionView { SelectionMode = SelectionMode.None, ItemTemplate = template };
            list.SetBinding(ItemsView.ItemsSourceProperty, new Binding(nameof(DynamicPageViewModel.RecordsView)));
            return list;
        }

        /// <summary>
        /// Таблица; если колонкам не хватает ширины (телефон), те же записи показываются карточками.
        /// Горизонтальная прокрутка не годится: в Windows экран с CollectionView шире горизонтального ScrollView
        /// молча не открывался (MAUI 10.0.110).
        /// </summary>
        private static View BuildTable(DynamicPageViewModel d)
        {
            var fields = d.ListFields.ToList();
            var minWidth = fields.Sum(f => f.FieldType == FieldType.Photo ? PhotoColumnWidth : MinColumnWidth);

            View table = BuildTableGrid(d, fields), cards = null;
            var host = new ContentView { Content = table };
            host.SizeChanged += (sender, e) =>
            {
                if (host.Width <= 0) return;
                var next = host.Width < minWidth ? cards ?? (cards = BuildCards(d)) : table;
                if (!ReferenceEquals(host.Content, next)) host.Content = next;
            };
            return host;
        }

        private static View BuildTableGrid(DynamicPageViewModel d, List<FieldDefinitionModel> fields)
        {

            var header = new Grid { Padding = new Thickness(0, 6), ColumnDefinitions = TableColumns(fields) };
            for (var i = 0; i < fields.Count; i++)
                header.Add(new Label
                {
                    Text = fields[i].DisplayName,
                    FontAttributes = FontAttributes.Bold,
                    Margin = new Thickness(14, 0, 10, 0),
                    LineBreakMode = LineBreakMode.TailTruncation,
                    HorizontalTextAlignment = IsNumeric(fields[i]) ? TextAlignment.End : TextAlignment.Start
                }, i, 0);

            var list = RecordsList(new DataTemplate(() => BuildTableRow(d, fields)));

            var root = new Grid { RowDefinitions = Rows(GridLength.Auto, GridLength.Auto, GridLength.Star) };
            root.Add(header, 0, 0);
            root.Add(new BoxView { HeightRequest = 1, Color = Colors.Gray, Opacity = 0.5 }, 0, 1);
            root.Add(list, 0, 2);
            return root;
        }

        private static View BuildTableRow(DynamicPageViewModel d, List<FieldDefinitionModel> fields)
        {
            var hasPhoto = fields.Any(f => f.FieldType == FieldType.Photo);
            var row = new Grid
            {
                Padding = new Thickness(0, 6),
                MinimumHeightRequest = hasPhoto ? 56 : 0,
                ColumnDefinitions = TableColumns(fields)
            };
            PaintBackground(row, d.Highlight);

            for (var i = 0; i < fields.Count; i++)
            {
                var f = fields[i];
                if (f.FieldType == FieldType.Photo)
                {
                    row.Add(PhotoThumbnail(f, PhotoThumbnailHeight, LayoutOptions.Center), i, 0);
                    continue;
                }

                var cell = new Label
                {
                    Margin = new Thickness(14, 0, 10, 0),
                    VerticalOptions = LayoutOptions.Center,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    HorizontalTextAlignment = IsNumeric(f) ? TextAlignment.End : TextAlignment.Start
                };
                cell.SetBinding(Label.TextProperty, ValueBinding(d, f));
                PaintText(cell, d.Highlight);
                row.Add(cell, i, 0);
            }

            var line = new VerticalStackLayout { Children = { row, new BoxView { HeightRequest = 1, Color = Colors.Gray, Opacity = 0.25 } } };
            return RecordActions(d, line);
        }

        private static View BuildCards(DynamicPageViewModel d)
        {
            var fields = d.ListFields.ToList();
            return RecordsList(new DataTemplate(() =>
            {
                var content = new VerticalStackLayout { Spacing = 2 };
                foreach (var f in fields)
                {
                    if (f.FieldType == FieldType.Photo)
                    {
                        content.Add(PhotoThumbnail(f, PhotoCardHeight, LayoutOptions.Start));
                        continue;
                    }

                    var value = new Span();
                    value.SetBinding(Span.TextProperty, ValueBinding(d, f));
                    var text = new Label
                    {
                        LineBreakMode = LineBreakMode.WordWrap,
                        FormattedText = new FormattedString { Spans = { new Span { Text = f.DisplayName + ": ", FontAttributes = FontAttributes.Bold }, value } }
                    };
                    PaintText(text, d.Highlight);
                    content.Add(text);
                }

                var card = new Border
                {
                    StrokeThickness = 1,
                    Stroke = Colors.Gray,
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Padding = new Thickness(10),
                    Content = content
                };
                PaintBackground(card, d.Highlight);
                // Отступ — у обёртки: Margin корня шаблона CollectionView в Windows не учитывает.
                return new ContentView { Padding = new Thickness(10, 5), Content = RecordActions(d, card) };
            }));
        }

        private static View BuildCustom(DynamicPageViewModel d)
        {
            var info = d.CustomViewInfo;
            if (info == null || !typeof(View).IsAssignableFrom(info.ControlType))
                return RecordsList(new DataTemplate(() => new Label { Text = Tr.T("Custom record view not found."), Margin = new Thickness(10) }));

            return RecordsList(new DataTemplate(() =>
            {
                var root = (View)Activator.CreateInstance(info.ControlType);
                root.Margin = new Thickness(10, 5);

                foreach (var b in d.CustomBindings)
                {
                    var fld = info.Properties.FirstOrDefault(x => x.PropertyName == b.ControlProperty);
                    var property = fld?.TargetProperty as BindableProperty;
                    if (property == null || string.IsNullOrEmpty(b.ModelProperty)) continue;

                    if (!string.IsNullOrEmpty(b.Header) && fld.PropertyType == typeof(string))
                        root.SetBinding(property, new Binding(b.ModelProperty, stringFormat: b.Header + "{0}"));
                    else root.SetBinding(property, new Binding(b.ModelProperty));
                }

                return RecordActions(d, new ContentView { Content = root });
            }));
        }

        /// <summary>Тап — открыть запись; свайп влево и контекстное меню — изменить или удалить.</summary>
        private static View RecordActions(DynamicPageViewModel d, View content)
        {
            var tap = new TapGestureRecognizer { Command = d.EditRecordCommand };
            tap.SetBinding(TapGestureRecognizer.CommandParameterProperty, new Binding("."));
            content.GestureRecognizers.Add(tap);

            var menu = new MenuFlyout();
            menu.Add(RecordMenuItem(Tr.T("Edit"), d.EditRecordCommand));
            menu.Add(RecordMenuItem(Tr.T("Delete"), d.DeleteRecordCommand));
            FlyoutBase.SetContextFlyout(content, menu);

            if (!d.CanDelete) return content;

            var delete = new SwipeItem { Text = Tr.T("Delete"), BackgroundColor = Colors.IndianRed, Command = d.DeleteRecordCommand };
            delete.SetBinding(SwipeItem.CommandParameterProperty, new Binding("."));
            return new SwipeView { Content = content, RightItems = new SwipeItems { delete } };
        }

        private static MenuFlyoutItem RecordMenuItem(string text, RelayCommand command)
        {
            var item = new MenuFlyoutItem { Text = text, Command = command };
            item.SetBinding(MenuItem.CommandParameterProperty, new Binding("."));
            return item;
        }

        private static Image PhotoThumbnail(FieldDefinitionModel f, int height, LayoutOptions horizontal)
        {
            var image = new Image
            {
                HeightRequest = height,
                Aspect = Aspect.AspectFit,
                Margin = new Thickness(8, 4),
                HorizontalOptions = horizontal
            };
            image.SetBinding(Image.SourceProperty, new Binding(f.PropertyName, converter: new PhotoImageConverter(), converterParameter: height));
            return image;
        }

        private static Binding ValueBinding(DynamicPageViewModel d, FieldDefinitionModel f)
        {
            if (f.FieldType == FieldType.Reference) return new Binding(f.PropertyName, converter: new ReferenceLookupConverter(d, f.PropertyName));
            if (f.FieldType == FieldType.Computed) return new Binding(f.PropertyName, converter: new ComputedDisplayConverter(f.Computed));
            if (f.FieldType == FieldType.Date) return new Binding(f.PropertyName, stringFormat: "{0:" + DateFormat + "}");
            if (f.FieldType == FieldType.Bool) return new Binding(f.PropertyName, converter: new BoolDisplayConverter());
            return new Binding(f.PropertyName);
        }

        // Подсветка — через DataTrigger, как в WPF: у неподходящих записей остаются цвета из стилей приложения.
        private static void PaintBackground(VisualElement element, HighlightRuleModel rule)
        {
            var background = HighlightPainter.BackgroundFor(rule);
            if (background == null) return;
            element.Triggers.Add(HighlightTrigger(element.GetType(), rule, VisualElement.BackgroundColorProperty, background));
        }

        private static void PaintText(Label label, HighlightRuleModel rule)
        {
            var foreground = HighlightPainter.ForegroundFor(rule);
            if (foreground == null) return;
            label.Triggers.Add(HighlightTrigger(typeof(Label), rule, Label.TextColorProperty, foreground));
        }

        private static DataTrigger HighlightTrigger(Type targetType, HighlightRuleModel rule, BindableProperty property, Color color)
        {
            var trigger = new DataTrigger(targetType)
            {
                Binding = new Binding(rule.PropertyName, converter: new HighlightMatchConverter(rule)),
                Value = true
            };
            trigger.Setters.Add(new Setter { Property = property, Value = color });
            return trigger;
        }

        private static ColumnDefinitionCollection TableColumns(List<FieldDefinitionModel> fields) =>
            Columns(fields.Select(f => f.FieldType == FieldType.Photo ? new GridLength(PhotoColumnWidth) : GridLength.Star).ToArray());

        private static bool IsNumeric(FieldDefinitionModel f) => f.FieldType == FieldType.Number || f.FieldType == FieldType.Computed;

        #endregion

        #region Общее

        private static Label BuildErrorLabel()
        {
            var error = new Label
            {
                TextColor = Colors.Red,
                LineBreakMode = LineBreakMode.WordWrap,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(8, 0, 8, 4)
            };
            error.SetBinding(Label.TextProperty, new Binding(nameof(BaseContentViewModel.ErrorMessage)));
            error.SetBinding(VisualElement.IsVisibleProperty, new Binding(nameof(BaseContentViewModel.ErrorMessage), converter: NotEmptyConverter.Instance));
            return error;
        }

        private static FlexLayout ActionsPanel() => new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Center,
            Margin = new Thickness(0, 6, 0, 12)
        };

        private static Button ActionButton(string text, string commandPath)
        {
            var button = CommandButton(text, commandPath);
            button.MinimumWidthRequest = 130;
            button.Margin = new Thickness(6);
            // Во FlexLayout элементы по умолчанию сжимаются, и на Android надпись кнопки обрезается.
            FlexLayout.SetShrink(button, 0);
            return button;
        }

        private static Button CommandButton(string text, string commandPath)
        {
            var button = new Button { Text = text };
            button.SetBinding(Button.CommandProperty, new Binding(commandPath));
            return button;
        }

        private static T Bound<T>(T view, BindableProperty property, string path) where T : BindableObject
        {
            view.SetBinding(property, new Binding(path, BindingMode.TwoWay));
            return view;
        }

        private static readonly DateTime MinPickerDate = new DateTime(1, 1, 2);
        private static readonly DateTime MaxPickerDate = new DateTime(9999, 12, 30);

        /// <summary>
        /// Дата: из модели в календарь — привязкой, обратно — только выбор пользователя. DatePicker подрезает дату
        /// вне своего диапазона и двусторонняя привязка записала бы подрезанное в модель (9999-12-31 «бессрочно»
        /// превращалась бы в 9999-12-30). Ровно MinValue/MaxValue в диапазон не берём: календарь Windows переводит
        /// дату в DateTimeOffset и падает на краях.
        /// </summary>
        private static DatePicker DateEditor()
        {
            var picker = new DatePicker { Format = DateFormat, MinimumDate = MinPickerDate, MaximumDate = MaxPickerDate };
            picker.SetBinding(DatePicker.DateProperty, new Binding(nameof(DateFieldViewModel.Value), BindingMode.OneWay));
            picker.DateSelected += (sender, e) =>
            {
                var field = picker.BindingContext as DateFieldViewModel;
                if (field == null) return;
                var stored = field.Value;
                if (stored.HasValue && (stored.Value < MinPickerDate || stored.Value > MaxPickerDate)
                    && e.NewDate == (stored.Value < MinPickerDate ? MinPickerDate : MaxPickerDate)) return;
                field.Value = e.NewDate;
            };
            return picker;
        }

        private static RowDefinitionCollection Rows(params GridLength[] heights) =>
            new RowDefinitionCollection(heights.Select(h => new RowDefinition(h)).ToArray());

        private static ColumnDefinitionCollection Columns(params GridLength[] widths) =>
            new ColumnDefinitionCollection(widths.Select(w => new ColumnDefinition(w)).ToArray());

        #endregion
    }
}
