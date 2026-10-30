using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using XamlNavigator.Abstractions;
using XamlNavigator.Core;
using XamlNavigator.DynamicPages.FieldViewModels;
using XamlNavigator.Localization;
using XamlNavigator.Models;
using XamlNavigator.Platform;

namespace XamlNavigator.DynamicPages
{
    public class SearchFieldOption
    {
        public SearchFieldOption(string title, FieldDefinitionModel field)
        {
            Title = title;
            Field = field;
        }

        public string Title { get; }
        public FieldDefinitionModel Field { get; }
        public override string ToString() => Title;
    }

    /// <summary>
    /// Модель страницы, описанной в базе: форма записи (PageDisplayMode.Edit) или список записей
    /// (PageDisplayMode.List) с поиском, печатью, подсветкой и расчётными полями. Представление строит
    /// платформенная фабрика динамических страниц.
    /// </summary>
    public class DynamicPageViewModel : BaseContentViewModel
    {
        private readonly IDynamicPageRepository _repository;
        private readonly INavigationOrchestrator _navigator;
        private readonly PageDefinitionModel _page;
        private readonly List<FieldDefinitionModel> _fieldDefs;
        private readonly Type _modelType;
        private readonly Dictionary<ReferenceFieldViewModel, ReferenceSourceInfo> _referenceSources = new Dictionary<ReferenceFieldViewModel, ReferenceSourceInfo>();
        private readonly Dictionary<string, ForeignKeyNavigation> _navigationByFk = new Dictionary<string, ForeignKeyNavigation>();
        private readonly IAccessPolicy _accessPolicy;
        private readonly ComputedFieldEvaluator _computed;
        private object _pendingEditRecord;
        private object _editingRecord;
        private long _editingId;
        private int _pairedAccessLevel;
        private bool _pairedAccessKnown;
        private bool _isBusy;
        private IRecordsView _recordsView;

        public DynamicPageViewModel(
            IDynamicPageRepository repository
            , INavigationOrchestrator navigator
            , PageDefinitionModel page
            , List<FieldDefinitionModel> fields
            , Type modelType
            , CustomViewInfo customViewInfo
            , List<CustomViewBindingDto> customBindings
            , IAccessPolicy accessPolicy = null)
        {
            _repository = repository;
            _navigator = navigator;
            _accessPolicy = accessPolicy;
            _page = page;
            _modelType = modelType;
            CustomViewInfo = customViewInfo;
            CustomBindings = customBindings ?? new List<CustomViewBindingDto>();
            DisplayName = page.Title;
            _fieldDefs = fields.OrderBy(f => f.SortOrder).ToList();
            Fields = _fieldDefs.Select(BuildFieldViewModel).ToList();
            _computed = new ComputedFieldEvaluator(_modelType, _fieldDefs);

            foreach (var field in Fields.Where(f => f.FieldType != FieldType.Computed))
                field.PropertyChanged += (sender, e) => RecomputeFromForm();

            SearchFields = new List<SearchFieldOption> { new SearchFieldOption(Tr.T("All fields"), null) };
            SearchFields.AddRange(_fieldDefs.Select(f => new SearchFieldOption(f.DisplayName, f)));
            _searchField = SearchFields[0];
        }

        protected override async Task InitializeAsync()
        {
            if (PairedPageId != 0 && !_pairedAccessKnown)
            {
                var paired = await _repository.GetPageAsync(PairedPageId);
                _pairedAccessLevel = paired != null ? paired.RequiredAccessLevel : 0;
                _pairedAccessKnown = true;
                InvalidateCommands();
            }
        }

        protected override void OnInitializationFailed(Exception exception)
        {
            _initError = Tr.F("Couldn't open the page: {0}", exception.Message);
            ShowLoadErrors();
        }

        // Ошибки загрузки складываются: справочник, расчёт и открытие страницы не затирают сообщения друг друга,
        // а удачная перезагрузка убирает своё.
        private string _initError = "";
        private string _referenceError = "";
        private string _computedError = "";

        private void ShowLoadErrors() =>
            ErrorMessage = string.Join(Environment.NewLine, new[] { _initError, _referenceError, _computedError }.Where(e => !string.IsNullOrEmpty(e)));

        /// <summary>
        /// Справочники для полей-ссылок: названия для списка, варианты для выбора и строки для навигационных свойств.
        /// Читаются при каждом показе страницы — справочник могли изменить на другой странице.
        /// </summary>
        private async Task LoadReferencesAsync()
        {
            foreach (var def in _fieldDefs.Where(f => f.FieldType == FieldType.Reference
                                                      && !string.IsNullOrEmpty(f.ReferenceTable)))
            {
                var refType = ResolveType(def.ReferenceModelTypeName);
                if (refType == null) continue;

                var rows = await _repository.GetRowsAsync(def.ReferenceTable, refType);
                var idProp = refType.GetProperty("Id");
                if (idProp == null) throw new InvalidOperationException(Tr.F("Reference table \"{0}\" has no Id field.", def.ReferenceTable));
                var titleProp = refType.GetProperty(def.ReferenceTitleProperty ?? "Id");

                var byId = new Dictionary<long, object>();
                foreach (var row in rows) byId[Convert.ToInt64(idProp.GetValue(row), CultureInfo.InvariantCulture)] = row;

                ReferenceLookups[def.PropertyName] = byId.ToDictionary(
                    kv => kv.Key,
                    kv => titleProp?.GetValue(kv.Value)?.ToString() ?? "");

                var refVm = _referenceSources.Keys.FirstOrDefault(v => v.PropertyName == def.PropertyName);
                if (refVm != null)
                    refVm.Items = byId.Select(kv =>
                        new ReferenceListItem(kv.Key, titleProp?.GetValue(kv.Value)?.ToString() ?? "")).ToList();

                var navProp = NavigationPropertyFor(def.PropertyName, refType);
                if (navProp != null)
                    _navigationByFk[def.PropertyName] = new ForeignKeyNavigation(navProp.Name, refType, byId);
            }
        }

        /// <summary>
        /// Навигационное свойство для внешнего ключа: CategoryId → Category; иначе — единственное свойство нужного
        /// типа. Если таких несколько (два ключа на одну таблицу), а по имени не нашлось — не заполняем ничего.
        /// </summary>
        private System.Reflection.PropertyInfo NavigationPropertyFor(string foreignKey, Type refType)
        {
            if (foreignKey.Length > 2 && foreignKey.EndsWith("Id", StringComparison.Ordinal))
            {
                var byName = _modelType.GetProperty(foreignKey.Substring(0, foreignKey.Length - 2));
                if (byName != null && byName.PropertyType == refType && byName.CanWrite) return byName;
            }

            var candidates = _modelType.GetProperties().Where(p => p.PropertyType == refType && p.CanWrite).ToList();
            return candidates.Count == 1 ? candidates[0] : null;
        }

        /// <summary>Политика активной оболочки (её могли сменить после входа), иначе переданная при создании.</summary>
        private IAccessPolicy Policy => _navigator?.CurrentShell?.AccessPolicy ?? _accessPolicy;

        private bool CanOpenPairedPage => PairedPageId != 0 && (Policy == null || Policy.CanAccessLevel(_pairedAccessLevel));

        private Type ResolveType(string fullName) => fullName == null ? null : _repository.ResolveModelType(fullName);

        public override async Task OnAppearingAsync()
        {
            await base.OnAppearingAsync();
            if (IsInitialized) _initError = "";

            try
            {
                await LoadReferencesAsync();
                _referenceError = "";
            }
            catch (Exception ex) { _referenceError = Tr.F("Couldn't load reference tables: {0}", ex.Message); }
            ShowLoadErrors();

            var record = _pendingEditRecord;
            if (record != null)
            {
                _pendingEditRecord = null;
                _resetPending = false;
                LoadForEdit(record);
            }
            else if (_resetPending)
            {
                _resetPending = false;
                EditingId = 0;
                _editingRecord = null;
                ResetFields();
            }

            if (DisplayMode == PageDisplayMode.List) await LoadRecordsAsync();
        }

        /// <summary>Открыть запись на редактирование при следующем появлении страницы.</summary>
        public void RequestEdit(object record) => _pendingEditRecord = record;

        /// <summary>
        /// Открыть пустую форму новой записи при следующем появлении страницы. Возврат на страницу «назад» или
        /// после диалога введённое не стирает — только этот вызов (его делает NavigateToDynamicAsync без записи).
        /// </summary>
        public void RequestNew()
        {
            _pendingEditRecord = null;
            _resetPending = true;
        }

        private bool _resetPending;

        public PageDisplayMode DisplayMode => _page.DisplayMode;
        public ListViewMode ListViewMode => _page.ListViewMode;
        public string CustomViewKey => _page.CustomViewKey;
        public IReadOnlyList<FieldDefinitionModel> FieldDefinitions => _fieldDefs;
        public IReadOnlyList<FieldDefinitionModel> ListFields => _fieldDefs.Where(f => f.ShowInList).ToList();
        public HighlightRuleModel Highlight => _page.Highlight;
        public bool CanRead => (_page.Operations & PageOperations.Read) != 0;
        public bool CanCreate => (_page.Operations & PageOperations.Create) != 0;
        public bool CanUpdate => (_page.Operations & PageOperations.Update) != 0;
        public bool CanDelete => (_page.Operations & PageOperations.Delete) != 0;
        public long PageId => _page.Id;
        public long PairedPageId => _page.PairedPageId;
        public Type ModelType => _modelType;
        public List<FieldViewModel> Fields { get; private set; } = new List<FieldViewModel>();
        public List<object> Records { get; private set; } = new List<object>();

        /// <summary>Записи, прошедшие поиск, — то, что привязывается к списку на экране.</summary>
        public IEnumerable RecordsView => _recordsView?.Source;

        /// <summary>Идёт сохранение или удаление записи: кнопки формы и списка на это время недоступны.</summary>
        public bool IsBusy
        {
            get { return _isBusy; }
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
#pragma warning disable CS0618
                OnPropertyChanged(nameof(IsLoading));
#pragma warning restore CS0618
                InvalidateCommands();
            }
        }

        /// <summary>Прежнее имя <see cref="IsBusy"/>: загрузку списка оно никогда не отражало.</summary>
        [Obsolete("Set only while saving or deleting; use IsBusy.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsLoading => IsBusy;

        /// <summary>Прежнее имя <see cref="EditRecordCommand"/>.</summary>
        [Obsolete("Use EditRecordCommand.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public RelayCommand EditRowCommand => EditRecordCommand;

        /// <summary>Прежнее имя <see cref="ClearFormCommand"/>.</summary>
        [Obsolete("Use ClearFormCommand.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public RelayCommand ClearCommand => ClearFormCommand;

        public CustomViewInfo CustomViewInfo { get; }
        public List<CustomViewBindingDto> CustomBindings { get; }
        public Dictionary<string, Dictionary<long, string>> ReferenceLookups { get; private set; } = new Dictionary<string, Dictionary<long, string>>();

        public long EditingId
        {
            get { return _editingId; }
            set
            {
                if (_editingId == value) return;
                _editingId = value;
                OnPropertyChanged(nameof(EditingId));
                OnPropertyChanged(nameof(IsEditing));
                InvalidateCommands();
            }
        }
        public bool IsEditing => EditingId != 0;

        private RelayCommand _saveRecordCommand;
        public RelayCommand SaveRecordCommand => _saveRecordCommand ?? (_saveRecordCommand = new RelayCommand(async _ => await SaveRecordAsync(),
            _ => !IsBusy && (IsEditing ? CanUpdate : CanCreate)));

        private RelayCommand _clearFormCommand;
        /// <summary>Очистить поля формы.</summary>
        public RelayCommand ClearFormCommand => _clearFormCommand ?? (_clearFormCommand = new RelayCommand(_ => ResetFields(), _ => !IsBusy));

        private RelayCommand _deleteCurrentCommand;
        public RelayCommand DeleteCurrentCommand => _deleteCurrentCommand ?? (_deleteCurrentCommand = new RelayCommand(async _ => await DeleteCurrentAsync(),
            _ => !IsBusy && IsEditing && CanDelete));

        private RelayCommand _deleteRecordCommand;
        public RelayCommand DeleteRecordCommand => _deleteRecordCommand ?? (_deleteRecordCommand = new RelayCommand(async p => { if (p != null) await DeleteRowAsync(p); },
            p => p != null && CanDelete));

        private RelayCommand _editRecordCommand;
        public RelayCommand EditRecordCommand => _editRecordCommand ?? (_editRecordCommand = new RelayCommand(p =>
        {
            if (p != null && PairedPageId != 0) _ = _navigator.NavigateToDynamicAsync(PairedPageId, p);
        }, p => p != null && PairedPageId != 0 && (CanUpdate || CanRead)));

        private RelayCommand _addRecordCommand;
        public RelayCommand AddRecordCommand => _addRecordCommand ?? (_addRecordCommand = new RelayCommand(
            _ => { if (PairedPageId != 0) _ = _navigator.NavigateToDynamicAsync(PairedPageId); },
            _ => CanOpenPairedPage && CanCreate));

        private RelayCommand _goBackCommand;
        public RelayCommand GoBackCommand => _goBackCommand ?? (_goBackCommand = new RelayCommand(async _ => await _navigator.NavigateBackAsync(), _ => !IsBusy));

        public void LoadForEdit(object record)
        {
            var idProp = record.GetType().GetProperty("Id");
            EditingId = idProp != null ? Convert.ToInt64(idProp.GetValue(record)) : 0;
            _editingRecord = record;

            foreach (var f in Fields)
            {
                var prop = _modelType.GetProperty(f.PropertyName);
                if (prop != null) f.SetValue(prop.GetValue(record));
            }
        }

        private void RecomputeFromForm()
        {
            if (!_computed.HasComputedFields) return;

            try
            {
                var draft = Activator.CreateInstance(_modelType);
                if (IsEditing) _modelType.GetProperty("Id")?.SetValue(draft, EditingId);

                foreach (var f in Fields)
                {
                    var prop = _modelType.GetProperty(f.PropertyName);
                    if (prop == null) continue;

                    if (f.FieldType == FieldType.Computed)
                    {
                        // Агрегаты в форме не пересчитываются (нужны другие таблицы) — берём сохранённые; остальное посчитает Apply.
                        if (_editingRecord != null && f is ComputedFieldViewModel && ((ComputedFieldViewModel)f).Computed?.Kind == ComputedKind.Aggregate)
                            prop.SetValue(draft, prop.GetValue(_editingRecord));
                        continue;
                    }

                    object coerced;
                    string error;
                    if (TryCoerce(f.GetValue(), prop.PropertyType, out coerced, out error)) prop.SetValue(draft, coerced);
                }

                _computed.Apply(draft, false);

                foreach (var field in Fields.OfType<ComputedFieldViewModel>())
                    field.Value = _modelType.GetProperty(field.PropertyName)?.GetValue(draft);
            }
            catch (Exception ex) { ErrorMessage = Tr.F("Couldn't recalculate: {0}", ex.Message); }
        }

        private async Task SaveRecordAsync()
        {
            ErrorMessage = "";
            var missing = Fields.Where(f => f.IsRequired)
                .Where(f => string.IsNullOrWhiteSpace(f.GetValue()?.ToString()))
                .Select(f => f.DisplayName).ToList();
            if (missing.Any())
            {
                ErrorMessage = Tr.F("Fill in the required fields: {0}", string.Join(", ", missing));
                return;
            }

            IsBusy = true;
            try
            {
                var instance = Activator.CreateInstance(_modelType);
                // Правка: начинаем с исходной записи, чтобы не затереть поля модели, которых нет на форме.
                if (IsEditing && _editingRecord != null) CopySimpleProperties(_editingRecord, instance);
                if (IsEditing) _modelType.GetProperty("Id")?.SetValue(instance, EditingId);

                var invalid = new List<string>();
                foreach (var f in Fields)
                {
                    if (f.FieldType == FieldType.Computed) continue;

                    var prop = _modelType.GetProperty(f.PropertyName);
                    if (prop == null) continue;

                    object coerced;
                    string error;
                    if (!TryCoerce(f.GetValue(), prop.PropertyType, out coerced, out error))
                    {
                        invalid.Add(Tr.F("\"{0}\" — {1}", f.DisplayName, error));
                        continue;
                    }

                    string ruleError;
                    if (f.Validation != null && !f.Validation.IsSatisfiedBy(coerced, f.FieldType, out ruleError))
                    {
                        invalid.Add(Tr.F("\"{0}\" — {1}", f.DisplayName, ruleError));
                        continue;
                    }

                    prop.SetValue(instance, coerced);
                }

                if (invalid.Any())
                {
                    ErrorMessage = Tr.F("Fix these fields: {0}", string.Join("; ", invalid));
                    return;
                }

                if (IsEditing)
                {
                    await _repository.UpdateRowAsync(_page.TableName, _modelType, instance);
                    EditingId = 0;
                    _editingRecord = null;
                    ResetFields();
                    await LeaveAsync();
                }
                else
                {
                    var newId = await _repository.InsertRowAsync(_page.TableName, _modelType, instance);
                    if (newId == 0)
                    {
                        ErrorMessage = Tr.T("The record wasn't saved. Check the database connection and the length of the values you entered.");
                        return;
                    }

                    ResetFields();
                    await LeaveAsync();
                }
            }
            catch (Exception ex) { ErrorMessage = Tr.F("Error while saving: {0}", ex.Message); }
            finally { IsBusy = false; }
        }

        private async Task DeleteCurrentAsync()
        {
            if (!IsEditing) return;
            ErrorMessage = "";
            IsBusy = true;
            try
            {
                if (!await _repository.DeleteRowAsync(_page.TableName, EditingId))
                {
                    ErrorMessage = DeleteFailedMessage;
                    return;
                }

                EditingId = 0;
                _editingRecord = null;
                ResetFields();
                await LeaveAsync();
            }
            catch (Exception ex) { ErrorMessage = Tr.F("Error while deleting: {0}", ex.Message); }
            finally { IsBusy = false; }
        }

        private async Task DeleteRowAsync(object row)
        {
            ErrorMessage = "";
            IsBusy = true;
            try
            {
                var id = Convert.ToInt64(row.GetType().GetProperty("Id").GetValue(row));
                if (!await _repository.DeleteRowAsync(_page.TableName, id))
                {
                    ErrorMessage = DeleteFailedMessage;
                    return;
                }

                await LoadRecordsAsync();
            }
            catch (Exception ex) { ErrorMessage = Tr.F("Error while deleting: {0}", ex.Message); }
            finally { IsBusy = false; }
        }

        private async Task LoadRecordsAsync()
        {
            var records = await _repository.GetRowsAsync(_page.TableName, _modelType);

            foreach (var record in records)
                foreach (var kv in _navigationByFk)
                {
                    var fkProp = _modelType.GetProperty(kv.Key);
                    var fkVal = fkProp?.GetValue(record);
                    if (fkVal == null) continue;
                    var id = Convert.ToInt64(fkVal);
                    object nav;
                    if (kv.Value.RecordsById.TryGetValue(id, out nav))
                        _modelType.GetProperty(kv.Value.NavigationPropertyName)?.SetValue(record, nav);
                }

            if (_computed.HasComputedFields)
            {
                await _computed.PrepareAggregatesAsync(LoadRowsForAggregateAsync);
                foreach (var record in records) _computed.Apply(record);

                _computedError = _computed.Problems.Count == 0
                    ? ""
                    : Tr.F("Couldn't calculate: {0}", string.Join("; ", _computed.Problems));
                ShowLoadErrors();
            }

            await NavigatorPlatform.Dispatcher.InvokeAsync(() =>
            {
                Records = records;
                _recordsView = NavigatorPlatform.RecordsViewFactory.Create(Records, Matches);

                OnPropertyChanged(nameof(Records));
                OnPropertyChanged(nameof(RecordsView));
                OnPropertyChanged(nameof(RecordsSummary));
            });
        }


        #region Поиск, фильтр и печать

        private string _searchText = "";
        private SearchFieldOption _searchField;

        public string SearchText
        {
            get { return _searchText; }
            set
            {
                _searchText = value ?? "";
                OnPropertyChanged(nameof(SearchText));
                RefreshView();
            }
        }

        public SearchFieldOption SearchField
        {
            get { return _searchField; }
            set
            {
                _searchField = value;
                OnPropertyChanged(nameof(SearchField));
                RefreshView();
            }
        }

        public List<SearchFieldOption> SearchFields { get; private set; } = new List<SearchFieldOption>();

        public bool HasSearch => !string.IsNullOrWhiteSpace(SearchText);

        public string RecordsSummary
        {
            get
            {
                var total = Records.Count;
                if (!HasSearch) return total == 0 ? Tr.T("No records yet") : Tr.F("Records: {0}", total);
                return Tr.F("Found {0} of {1}", VisibleRecords().Count, total);
            }
        }

        /// <summary>Печать доступна на этой платформе.</summary>
        public bool CanPrint => NavigatorPlatform.Printer != null;

        private RelayCommand _clearSearchCommand;
        public RelayCommand ClearSearchCommand => _clearSearchCommand ?? (_clearSearchCommand = new RelayCommand(_ => SearchText = "", _ => HasSearch));

        private RelayCommand _printCommand;
        public RelayCommand PrintCommand => _printCommand ?? (_printCommand = new RelayCommand(_ => Print(), _ => !IsBusy && CanPrint));

        private void RefreshView()
        {
            _recordsView?.Refresh();
            OnPropertyChanged(nameof(HasSearch));
            OnPropertyChanged(nameof(RecordsSummary));
            InvalidateCommands();
        }

        /// <summary>Проходит ли запись текущий поиск.</summary>
        public bool Matches(object record)
        {
            if (!HasSearch) return true;
            var needle = SearchText.Trim();

            var one = SearchField != null && SearchField.Field != null;
            var fields = one ? new List<FieldDefinitionModel> { SearchField.Field } : _fieldDefs;

            foreach (var f in fields)
            {
                var text = DisplayValue(record, f);
                if (string.IsNullOrEmpty(text)) continue;

                if (text.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;

                // «1 500» в списке находится и по «1500»: разделитель разрядов — неразрывный пробел.
                var plain = WithoutSpaces(text);
                if (plain.IndexOf(WithoutSpaces(needle), StringComparison.CurrentCultureIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static string WithoutSpaces(string text) => text.Replace("\u00A0", "").Replace("\u202F", "").Replace(" ", "");

        /// <summary>Записи, прошедшие поиск, в порядке показа.</summary>
        public List<object> VisibleRecords()
        {
            if (_recordsView == null) return Records;
            return _recordsView.GetVisibleRecords().ToList();
        }

        public string DisplayValue(object record, FieldDefinitionModel field)
        {
            if (record == null || field == null) return "";

            var prop = _modelType.GetProperty(field.PropertyName);
            var raw = prop?.GetValue(record);
            if (raw == null) return "";

            if (field.FieldType == FieldType.Computed) return ComputedFieldEvaluator.Format(raw, field.Computed);

            if (field.FieldType == FieldType.Reference)
            {
                Dictionary<long, string> map;
                if (!ReferenceLookups.TryGetValue(field.PropertyName, out map)) return "";
                long id;
                if (!long.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out id)) return "";
                string title;
                return map.TryGetValue(id, out title) ? title : "";
            }

            if (raw is bool) return (bool)raw ? Tr.T("Yes") : Tr.T("No");
            if (raw is DateTime) return ((DateTime)raw).ToString("dd.MM.yyyy", CultureInfo.CurrentCulture);
            return Convert.ToString(raw, CultureInfo.CurrentCulture);
        }

        /// <summary>То, что сейчас в списке, в виде таблицы для печати или выгрузки.</summary>
        public Report BuildReport()
        {
            var printed = _fieldDefs.Where(f => f.FieldType != FieldType.Photo).ToList();

            var columns = printed
                .Select(f => new ReportColumn(f.DisplayName, f.FieldType == FieldType.Number || f.FieldType == FieldType.Computed))
                .ToList();

            var rows = VisibleRecords()
                .Select(r => (IList<string>)printed.Select(f => DisplayValue(r, f)).ToList())
                .ToList();

            var subtitle = HasSearch
                ? Tr.F("Filter: \"{0}\"", SearchText.Trim())
                  + (SearchField != null && SearchField.Field != null ? Tr.F(" in the \"{0}\" field", SearchField.Field.DisplayName) : "")
                : null;

            return new Report(DisplayName, subtitle, columns, rows);
        }

        private void Print()
        {
            var printer = NavigatorPlatform.Printer;
            if (printer == null) return;

            try { printer.Print(BuildReport()); }
            catch (Exception ex) { ErrorMessage = Tr.F("Couldn't print: {0}", ex.Message); }
        }

        #endregion

        private async Task<List<object>> LoadRowsForAggregateAsync(string tableName, string modelTypeName)
        {
            var type = ResolveType(modelTypeName);
            if (type == null) return null;
            return await _repository.GetRowsAsync(tableName, type);
        }

        private static string DeleteFailedMessage => Tr.T("This record can't be deleted: records in another table refer to it. Delete or reassign them first.");

        private void ResetFields()
        {
            foreach (var f in Fields) f.Reset();
            InvalidateCommands();
        }

        private static void InvalidateCommands() => RelayCommand.InvalidateRequerySuggested();

        /// <summary>
        /// «Назад» после сохранения или удаления — только если форма всё ещё на экране: пока шло сохранение,
        /// пользователь мог уйти сам (аппаратная «Назад», меню), и второй шаг назад увёл бы дальше нужного.
        /// </summary>
        private Task LeaveAsync() =>
            ReferenceEquals(_navigator?.CurrentShell?.CurrentView?.ViewModel, this) ? _navigator.NavigateBackAsync() : Task.CompletedTask;

        /// <summary>
        /// Скопировать простые свойства (числа, строки, даты, перечисления) — без навигационных объектов, чтобы
        /// репозиторий на ORM не принял их за новые связанные записи.
        /// </summary>
        private void CopySimpleProperties(object source, object target)
        {
            if (!_modelType.IsInstanceOfType(source)) return;
            foreach (var prop in _modelType.GetProperties())
            {
                if (!prop.CanRead || !prop.CanWrite || prop.GetIndexParameters().Length > 0 || !IsSimple(prop.PropertyType)) continue;
                prop.SetValue(target, prop.GetValue(source));
            }
        }

        private static bool IsSimple(Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime)
                   || t == typeof(DateTimeOffset) || t == typeof(TimeSpan) || t == typeof(Guid) || t == typeof(byte[]);
        }

        /// <summary>Привести введённое значение к типу свойства модели; false — с текстом ошибки в <paramref name="error"/>.</summary>
        public static bool TryCoerce(object raw, Type target, out object value, out string error)
        {
            error = null;
            var t = Nullable.GetUnderlyingType(target) ?? target;
            var s = raw != null ? raw.ToString() : null;

            if (string.IsNullOrWhiteSpace(s))
            {
                bool nullable = !target.IsValueType || Nullable.GetUnderlyingType(target) != null;
                value = nullable ? null : Activator.CreateInstance(t);
                return true;
            }

            s = s.Trim();

            if (t == typeof(string))
            {
                value = s;
                return true;
            }

            if (t == typeof(bool))
            {
                bool b;
                if (bool.TryParse(s, out b))
                {
                    value = b;
                    return true;
                }
                value = false;
                error = Tr.T("expected \"true\" or \"false\"");
                return false;
            }

            if (t == typeof(DateTime))
            {
                DateTime d;
                if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.None, out d)
                    || DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                {
                    value = d;
                    return true;
                }
                value = default(DateTime);
                error = Tr.T("expected a date");
                return false;
            }

            if (t.IsEnum)
            {
                try
                {
                    value = Enum.Parse(t, s, true);
                    return true;
                }
                catch
                {
                    value = Activator.CreateInstance(t);
                    error = Tr.T("value is not one of the allowed values");
                    return false;
                }
            }

            if (IsNumeric(t)) return TryCoerceNumber(s, t, out value, out error);

            try
            {
                value = Convert.ChangeType(s, t, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                value = t.IsValueType ? Activator.CreateInstance(t) : null;
                error = Tr.F("couldn't convert to type {0}", t.Name);
                return false;
            }
        }

        private static bool IsNumeric(Type t) =>
            t == typeof(byte) || t == typeof(short) || t == typeof(int) || t == typeof(long)
            || t == typeof(float) || t == typeof(double) || t == typeof(decimal);

        private static bool TryCoerceNumber(string s, Type t, out object value, out string error)
        {
            error = null;

            var normalized = WithoutSpaces(s).Replace(',', '.');

            if (t == typeof(decimal))
            {
                decimal dec;
                if (decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out dec)) { value = dec; return true; }
                value = 0m;
                error = Tr.T("expected a number, e.g. 12.5");
                return false;
            }

            if (t == typeof(double) || t == typeof(float))
            {
                double dbl;
                if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out dbl))
                {
                    value = t == typeof(float) ? (object)(float)dbl : dbl;
                    return true;
                }
                value = Activator.CreateInstance(t);
                error = Tr.T("expected a number, e.g. 12.5");
                return false;
            }

            long whole;
            if (long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out whole))
            {
                try
                {
                    value = Convert.ChangeType(whole, t, CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    value = Activator.CreateInstance(t);
                    error = Tr.F("value is out of range for type {0}", t.Name);
                    return false;
                }
            }

            value = Activator.CreateInstance(t);
            error = normalized.Contains(".") ? Tr.T("expected a whole number without a decimal part") : Tr.T("expected a whole number");
            return false;
        }

        private FieldViewModel BuildFieldViewModel(FieldDefinitionModel f)
        {
            if (f.FieldType == FieldType.Computed) return new ComputedFieldViewModel(f);
            if (f.FieldType != FieldType.Reference || string.IsNullOrEmpty(f.ReferenceTable)) return CreateSimpleField(f);

            var refType = ResolveType(f.ReferenceModelTypeName);
            if (refType == null) return CreateSimpleField(f);
            var vm = new ReferenceFieldViewModel(f, f.ReferenceTitleProperty ?? "Id");
            _referenceSources[vm] = new ReferenceSourceInfo
            {
                Table = f.ReferenceTable,
                Type = refType
            };

            return vm;
        }

        private FieldViewModel CreateSimpleField(FieldDefinitionModel f)
        {
            switch (f.FieldType)
            {
                case FieldType.Number: return new NumberFieldViewModel(f);
                case FieldType.Date: return new DateFieldViewModel(f);
                case FieldType.Bool: return new BoolFieldViewModel(f);
                case FieldType.Choice: return new ChoiceFieldViewModel(f, ParseOptions(f.OptionsJson));
                case FieldType.MultiLine: return new MultiLineFieldViewModel(f);
                case FieldType.Computed: return new ComputedFieldViewModel(f);
                case FieldType.Photo: return new PhotoFieldViewModel(f);
                default: return new TextFieldViewModel(f);
            }
        }

        private static List<string> ParseOptions(string json)
        {
            if (string.IsNullOrEmpty(json)) return new List<string>();
            try { return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>(); }
            catch { return new List<string>(); }
        }

        private sealed class ReferenceSourceInfo
        {
            public string Table { get; set; }
            public Type Type { get; set; }
        }

        /// <summary>
        /// Как заполнить навигационное свойство ORM по внешнему ключу (CategoryId → Category): имя свойства
        /// и загруженные записи справочника по Id.
        /// </summary>
        private sealed class ForeignKeyNavigation
        {
            public ForeignKeyNavigation(string navigationPropertyName, Type referenceType, Dictionary<long, object> recordsById)
            {
                NavigationPropertyName = navigationPropertyName;
                ReferenceType = referenceType;
                RecordsById = recordsById;
            }

            public string NavigationPropertyName { get; }
            public Type ReferenceType { get; }
            public Dictionary<long, object> RecordsById { get; }
        }
    }
}
