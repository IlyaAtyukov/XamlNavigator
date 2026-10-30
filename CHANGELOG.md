# Изменения

## 1.0.0-preview.1 — 2026-10-28

Первая публичная версия. Объединяет `WpfNavigator` (полная версия с динамическими страницами из шаблона для демоэкзамена) и `MauiNavigator` в одно ядро и два платформенных пакета.

### Устройство
- Ядро `XamlNavigator` не зависит от UI (net461, netstandard2.0, net8.0); всё платформенное — за интерфейсами `XamlNavigator.Platform` и подключается `WpfPlatform.Install()` / `MauiPlatform.Install()` (вызываются сами при `NavigatorBuilder.Build()` и при первом использовании платформенных типов).
- Общие имена моделей: `BaseShellViewModel` (оболочка) и `BaseContentViewModel` (экран). Прежние имена остаются: `BaseWindowViewModel`, `BaseUserControlViewModel` (WPF), `BasePageViewModel` (MAUI).
- `CurrentView` — `ViewPair<object, BaseContentViewModel>`; привязки `{Binding CurrentView.View}` работают как раньше.
- Динамические страницы теперь есть и в MAUI.
- `NavigatorBuilder.UseViewsFrom(assembly)` — модели в общей библиотеке, представления в приложении.
- Строки библиотеки — на английском (они же ключи перевода); русский перевод встроен и выбирается сам по `CurrentUICulture`. Настройка — `NavigatorLocalization` (`Language`, `Translator`, `AddTranslations`); полноту перевода проверяет тест.

### Новое
- `INavigationOrchestrator.OpenAsync<T>()`, `CreateRootView<T>()`, `CurrentShell`, `HistoryChanged`; `NavigationOrchestrator.ReportFailure()`; `NavigatorBuilder.ClearHistoryOnMenuNavigation()`.
- `BaseShellViewModel.RefreshAccess()` — пересобрать меню после смены прав; смена `AccessPolicy` делает это сама. `BaseShellViewModel.NavigationFailed` — ошибки переходов по меню (оркестратор пересылает их в свой `NavigationFailed`).
- `NavigationCluster.IsFirst` — прятать разделитель над первой группой меню.
- `BaseShellViewModel.OnShellAppearingAsync()` / `OnShellDisappearingAsync()` — платформа сообщает о показе и уходе окна/страницы; текущий экран получает `OnAppearingAsync` ровно один раз.
- `RelayCommand.ExecuteAsync()`, `RaiseCanExecuteChanged()`, `IsExecuting`, статические `InvalidateRequerySuggested()`, `ExecutionFailed` и `ReportFailure()` — исключения асинхронных команд можно получить подпиской (раньше узнать о них было нельзя).
- `NavigatorPlatform.TryEnsureInstalled()`; платформа подключается уже при первом создании `RelayCommand`.
- WPF: `DynamicViewFactory.ResetFieldTemplates()` — пересобрать шаблоны полей формы после смены языка (смена `NavigatorLocalization.Translator` или `CurrentUICulture` замечается сама).
- `BaseViewModel.SetProperty`, `OnInitializationFailed`, `IsInitialized`; `BaseContentViewModel.HasParam<T>()`.
- `NavigationItem.IsSelected` и `ViewModelType` — подсветка текущего пункта меню.
- `DynamicPageViewModel.RequestNew()`, `BuildReport()`, `CanPrint`, `Matches()`, `VisibleRecords()`, `TryCoerce()`; `ComputedFieldEvaluator.PrepareAggregatesAsync()`.
- `IDynamicViewFactory.Register(pageId, factory)` без уровня доступа — метод-расширение для прежнего кода.
- `NavigatorBuilder.OnNavigationFailed(Action<INavigationOrchestrator>)` — прежняя форма из Stronghold.
- WPF: тема по умолчанию для `DialogHostWindow`; xmlns `https://github.com/IlyaAtyukov/XamlNavigator`.
- Необязательные параметры конструктора модели получают значения по умолчанию, если их нет в контейнере.

### Исправлено (по сравнению с WpfNavigator и MauiNavigator)
Навигация:
- Первый экран оболочки без стартового экрана получал `OnAppearingAsync` дважды (от перехода и от Loaded/Appearing).
- Стартовый экран показывался, даже если политика доступа его запрещала.
- Неудачный переход или переход на уже открытый экран оставлял запись в истории «назад».
- Экран, который не создался с первого раза, оставался сломанным до конца жизни окна (исключение запоминалось).
- Ошибки переходов по пунктам меню и `NavigateParentTo(Async)` пропадали, отказ по правам в нём был молчаливым; теперь и то и другое приходит в `NavigationFailed`.
- Двойной щелчок по пункту меню запускал переход дважды (два `OnDisappearingAsync` у прежнего экрана).
- Смена `AccessPolicy` у оболочки пересобирала меню, но переходы проверялись прежней политикой; теперь везде — политика активной оболочки.
- Неудачный показ окна теперь возвращает прежнюю оболочку вместе с её подписками и историей «назад».
- Если первый пункт меню — динамическая страница, она открывалась в прежнем окне, а не в новом.
- Повторный выбор открытого пункта меню (и обычного, и динамической страницы) не вызывает у экрана `OnDisappearingAsync` и не пишет историю.
- Кнопки, привязанные к командам до подключения платформы, переставали перепроверять доступность.
- Пункт меню динамической страницы не слышал `RaiseCanExecuteChanged()` исходной команды (MAUI).
- `CommandManager.InvalidateRequerySuggested()` из фонового потока терялся; теперь уходит в поток интерфейса.
- Необязательный параметр `DateTime x = default` в конструкторе модели ронял создание экрана на .NET Framework.

Динамические страницы:
- Команды `DynamicPageViewModel` создавались заново при каждом чтении свойства (`_x ?? new RelayCommand(...)` без присваивания).
- Сохранение правки затирало поля модели, которых нет на форме.
- Возврат на форму новой записи «назад» (и в MAUI — закрытие диалога над ней) стирал введённое.
- Два внешних ключа на одну таблицу заполняли одно и то же навигационное свойство.
- Справочники читались один раз за жизнь страницы; теперь — при каждом показе. Если справочник не загрузился, правка записи больше не сбрасывает её ссылку, а ошибка видна на форме.
- Расчётные поля типа `decimal`, `int`, `float` не заполнялись (значение писалось как `double`).
- Порог подсветки «1,5» читался как 15, «1 500» — как текст.
- Агрегаты расчётных полей грузились синхронно (`GetAwaiter().GetResult()`) в потоке интерфейса — риск зависания с настоящей базой.
- Круговые ссылки расчётных полей пропадали из списка проблем после первого подсчёта.
- После сохранения отредактированной записи форма помнила её и подставляла старые расчётные значения в следующую новую.
- Сообщение о неудачном удалении оставалось после удачного; «Назад» во время сохранения срабатывал дважды.
- Повторный выбор того же фото из другой папки падал с «файл уже существует»; другое фото с тем же именем и размером считалось тем же файлом.
- `PageRecordModel` не замечал смену `ValuesJson` после первого чтения.

WPF:
- Таблица без стиля приложения позволяла править ячейки и показывала пустую строку «новая запись», хотя такие правки никуда не сохранялись.
- Числа в таблице и карточках выводились в формате en-US, а поиск и печать — в текущей культуре.
- Логические поля в таблице и карточках показывались как True/False; теперь «Да/Нет», как в поиске и печати.
- Двойной щелчок по заголовку таблицы или полосе прокрутки открывал выделенную запись.
- Карточки не учитывали цвет текста из правила подсветки; стили таблицы брали основу только из ресурсов приложения.
- Закрытие диалога без владельца завершало приложение в режиме `OnMainWindowClose`; восстановление режима падало, если приложение уже завершалось.
- Esc в диалоге закрывал весь диалог, даже когда был раскрыт список.
- `InitializeAsync` модели диалога не вызывался.
- `OnDisappearingAsync` вызывался и при закрытии окна, отменённом приложением; теперь — по `Closed`, вместе с текущим экраном.
- Переведённые строки в шаблонах полей формы застывали при первой загрузке.

MAUI:
- Исключение в обработчиках Appearing/Disappearing роняло приложение; теперь приходит в `NavigationFailed`.
- Модальный диалог посылал экрану под собой `OnDisappearingAsync`/`OnAppearingAsync`.
- `DatePicker` подрезал даты вне 1900–2100 и записывал подрезанное в модель.
- Поле «Фото» показывало пустое место вместо «не удалось открыть» для файлов, которые не являются картинками.

### Изменено
- Команды пунктов меню (`NavigationItem.Command`) синхронные, как в прежнем WpfNavigator (в MauiNavigator были асинхронными).
- Заглушка пустого окна по умолчанию нейтральная; текст про «Конструктор сущностей» задаёт приложение.
- `AddDynamicNavigationItem`: по сравнению с Stronghold четвёртым параметром стал `requiredAccessLevel` (группу, порядок и иконку передавайте по имени).
- Пустой результат расчёта в ненулевое свойство записывается нулём, а не оставляет прежнее значение; текст проблемы — «значение не посчитано».

### Переименовано (прежние имена работают как [Obsolete], кроме отмеченных)
Имена приведены к тому, за что отвечают типы и члены.

| Было | Стало | Почему |
|---|---|---|
| `L.T` / `L.F` / `L.Translator` | `NavigatorLocalization.Translate` / `Format` / `Translator` | однобуквенный тип не говорил, что это настройка языка |
| `FieldDefinitionModel.FieldName`, `FieldViewModel.FieldName` | `DisplayName` | это подпись для пользователя, а не идентификатор; в JSON имя прежнее — `FieldName` |
| `FieldDefinitionModel.Options` | `OptionsJson` | значение — текст JSON-массива; в JSON имя прежнее — `Options` |
| `FieldType.List`, `ListFieldViewModel` | `FieldType.Choice` (то же значение 5), `ChoiceFieldViewModel`* | «List» в этом API — страница-список |
| `BaseContentViewModel.ParentPage` / `SetParentPage` | `ParentShell` / внутренний `SetParentShell` | это окно или страница-оболочка, не «страница» динамического списка |
| `SetCurrentView…`, `SetArbitraryView`, `NavigateParentTo…` | `SetCurrentViewAsync…`, `NavigateParentToAsync…` | возвращают Task и выполняют жизненный цикл экранов |
| `GetParam<T>()` | `TakeParam<T>()` | чтение одноразовое: параметр очищается |
| `ChildOfAttribute.Visible` | `ShowInMenu` | при false экран не скрыт — у него просто нет пункта меню |
| `NavigationItem.Tag` | `DynamicPageId` (long?) | там всегда номер динамической страницы |
| `NavigationCluster.Index` | `Number` (+ `IsFirst`) | это номер группы, а не позиция в списке |
| `DynamicPageViewModel.IsLoading` | `IsBusy` | ставится только на время сохранения и удаления |
| `EditRowCommand`, `ClearCommand` (страница) | `EditRecordCommand`, `ClearFormCommand` | единообразно с *Record* и без путаницы с `ClearSearchCommand` |
| `ViewTypeModel` | `ViewTypePair` | регистрация пары типов, а не модель |
| `CustomViewField`, `CustomViewInfo.Fields` | `CustomViewProperty`, `Properties` | свойство элемента, а не поле сущности |
| `FieldValidationModel.WholeValue` | `ToFullMatchPattern` | оборачивает шаблон в ^(?:…)$ |
| `ReferenceFieldViewModel.DisplayMember` | `TitleProperty` | имя свойства-названия записи справочника |
| `PhotoStore.Forget()` | `InvalidateIndex()` | сбрасывает кэш списка файлов |
| `PhotoStore.FolderName` (константа «Фото») | настраиваемое свойство, по умолчанию «Photos» | |
| `BaseContentViewModel.NavigationIcon` | — (устарело) | меню его не читало; иконка — `[ChildOf(IconKey)]` |
| `PageDisplayMode.Both` | — (устарело) | не поддерживался, показывался как Edit |
| `IDynamicViewFactory.GetView`, `IStaticViewFactory.GetShellView/GetContentView/GetChildViews`* | `CreateView`, `CreateShellView`, `CreateContentView`, `GetChildViewTypes` | каждый вызов создаёт новую пару |
| `DynamicViewFactoryBase.WithoutViews`* | `NullDynamicViewFactory` | |
| `NavigatorPlatform.Views/RecordsViews/Photos/DynamicViewFactory`* | `ViewAdapter`, `RecordsViewFactory`, `PhotoService`, `DefaultDynamicViewFactory` | одно свойство — один сервис |
| `IPhotoPlatform`, `Wpf/MauiPhotoPlatform`, `ForgetImages`* | `IPhotoService`, `Wpf/MauiPhotoService`, `ClearImageCache` | «Platform» — только установщики платформ |
| `PhotoImage`* (WPF), `WpfDispatcher`* | `PhotoImageLoader`, `WpfUiDispatcher` | |
| `IRecordsView.Visible()`* | `GetVisibleRecords()` | |
| вложенные `DynamicPageViewModel.NavigationSource` / `ReferenceSourceInfo`* | закрыты (`private`) | детали реализации |

\* без псевдонима: ни одно из трёх приложений-потребителей эти имена не использует.
### Убрано
- `DynamicListTemplateRegistry` — нигде не использовался.
- `IStaticViewFactory.GetWindowView` / `GetContentView<T>` с типами WPF → `CreateShellView(Type, …)` / `CreateContentView(Type, …)`; события `IStaticViewFactory.NavigationFailed` / `StaticViewFactory.NavigationFailed` (не вызывались) → `INavigationOrchestrator.NavigationFailed`.
- `IViewBinder.CreateAndBindWindowView` → `CreateAndBindShellView`; `CreateAndBindContentView(model, parent)` → `(model, orchestrator, parent)`.
- `INavigationOrchestrator.OpenWindowAsync` / `OpenPageAsync` / `CreateRootPage` больше не члены интерфейса, а методы-расширения платформ.
- MAUI и Stronghold: `NotValidCurrentView()` — оболочка без стартового экрана открывает первый пункт меню.
- MAUI: `BasePageViewModel.RestoreView()` → `SetCurrentViewAsync(view)`; `NavigatorBuilder.ViewTypeFor()` → `ViewLocator.ViewTypeFor()`; `StaticViewFactory.GetPage<T>()` → `CreateShellView(typeof(T), …)`; `ViewBinder.CreateAndBindPage` / `CreateAndBindContent` → `CreateAndBindShellView` / `CreateAndBindContentView`; экземплярный `ViewBinder.Create(type, orchestrator)` → `CreateInstance(type, orchestrator)`.
