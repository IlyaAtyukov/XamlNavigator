# Переход с WpfNavigator и MauiNavigator

API сохранён настолько, насколько позволило объединение: шаблон для демоэкзамена (полный WpfNavigator, net461) собирается с XamlNavigator.Wpf **заменой пространств имён** в коде и одной правкой `assembly=` в XAML. Прежние имена, которые переименованы для ясности, продолжают работать как `[Obsolete]` — компилятор подскажет новое имя предупреждением CS0618 (список — в [CHANGELOG.md](CHANGELOG.md), раздел «Переименовано»).

## Язык и папка фото — проверьте при запуске

- **Строки библиотеки теперь английские**, русский перевод встроен и включается по языку интерфейса (`CurrentUICulture`). Если программа должна быть русской на любой Windows (экзаменационные машины бывают с английской системой), задайте язык явно при запуске:
  ```csharp
  NavigatorLocalization.Language = "ru";
  ```
- **Папка фото по умолчанию — «Photos»** (была «Фото»). Если фото уже лежат в «Фото», задайте при запуске:
  ```csharp
  PhotoStore.FolderName = "Фото";
  ```

## Где теперь лежат типы (для XAML)

Код ссылается на пространства имён, и замены `WpfNavigator.` / `MauiNavigator.` → `XamlNavigator.` ему достаточно. В XAML важна ещё и **сборка**: прежняя одна сборка разделилась на ядро и платформу.

| Пространство имён | Сборка `XamlNavigator` (ядро) | Сборка платформы (`XamlNavigator.Wpf` / `XamlNavigator.Maui`) |
|---|---|---|
| `XamlNavigator.Models` | всё: `NavigationItem`, `NavigationCluster`, `ViewPair`… | — |
| `XamlNavigator.Core` | `BaseViewModel`, `BaseShellViewModel`, `BaseContentViewModel`, `RelayCommand`… | `BaseWindowViewModel`, `BaseUserControlViewModel` / `BasePageViewModel`, `UiThread` |
| `XamlNavigator.Services` | оркестратор, `NavigatorBuilder`, `ViewBinder`, `ViewLocator` | `DialogService`, `DialogHostWindow` / `DialogHostPage` |
| `XamlNavigator.DynamicPages` | `DynamicPageViewModel`, `ComputedFieldEvaluator`, `PhotoStore` | `DynamicViewFactory`, конвертеры, `ReportBuilder`, `PhotoImageLoader` |
| `XamlNavigator.DynamicPages.FieldViewModels`, `.Attributes`, `.Abstractions`, `.Localization`, `.Platform` | да | `.Abstractions` — методы-расширения, `.Platform` — `WpfPlatform` / `MauiPlatform` и их сервисы |
| `XamlNavigator.Controls` | — | `NavigatorPage` (MAUI) |

Пример: `xmlns:models="clr-namespace:XamlNavigator.Models;assembly=XamlNavigator"`, `xmlns:services="clr-namespace:XamlNavigator.Services;assembly=XamlNavigator.Wpf"`. В WPF для типов из платформенной сборки (`DialogHostWindow`, конвертеры) можно писать `xmlns:nav="https://github.com/IlyaAtyukov/XamlNavigator"`.

## WPF (WpfNavigator)

1. Удалите проект `WpfNavigator` из решения и ссылку на него.
2. Подключите пакет:
   - SDK-проект: `dotnet add package XamlNavigator.Wpf --prerelease`;
   - старый csproj с `packages.config` (net461): *Управление пакетами NuGet → XamlNavigator.Wpf* — ядро `XamlNavigator` и Newtonsoft.Json встанут отдельными ссылками, это нужно: компилятор разметки WPF в старых проектах не видит транзитивные сборки.
3. Замените в `.cs` и `.xaml`: `WpfNavigator.` → `XamlNavigator.`; в XAML проверьте `assembly=` по таблице выше (стиль `DialogHostWindow` — `assembly=XamlNavigator.Wpf`).
4. Задайте язык и папку фото (раздел выше), если нужно прежнее поведение.
5. Проверьте поведение, которое поменялось:
   - Заглушка пустого окна по умолчанию нейтральная («No pages yet / Pages you have access to will appear here», по-русски — «Страниц пока нет / Здесь появятся экраны, доступные вам»). Прежний текст про «Конструктор сущностей» задайте в модели главного окна (`EmptyContentTitle` / `EmptyContentMessage`).
   - `ServiceProviderAdapter.GetService` возвращает `null` для незарегистрированного сервиса (как `IServiceProvider`); исключение с именем недостающей зависимости бросает навигатор при создании модели.
   - Модель диалога получает `InitializeAsync` / `OnAppearingAsync` / `OnDisappearingAsync`; при закрытии окна `OnDisappearingAsync` получает и текущий экран.
   - Форма динамической страницы очищается, когда её открывают для новой записи, а не при каждом появлении: возврат «назад» введённое сохраняет.
   - Логические поля динамических страниц в таблице и карточках — «Да/Нет» («Yes/No»); числа — в текущей культуре.
   - `NavigationCluster.Index` — это номер группы, а не позиция: триггер «спрятать разделитель над первой группой» (`{Binding Index}` = 0) работает, пока первая группа имеет номер 0; надёжнее — `{Binding IsFirst}`.
   - `CustomViewField` → `CustomViewProperty` (`TargetProperty`, тип `object`; конструктор принимает `DependencyProperty` как раньше), `CustomViewInfo.Fields` → `Properties`.
   - `IDynamicViewFactory.GetView` → `CreateView`, возвращает `ViewPair<object, BaseContentViewModel>`; `WindowModel<,>` → `ViewPair<,>`, `WindowTypeModel` → `ViewTypePair`.
   - `IViewBinder.CreateAndBindWindowView` → `CreateAndBindShellView`, у `CreateAndBindContentView` добавился параметр `orchestrator`; событий `NavigationFailed` у `IStaticViewFactory` больше нет.
   - `OpenWindowAsync<T>()` — метод-расширение: нужен `using XamlNavigator.Abstractions;` (обычно уже есть).
   - `ReportColumn` переехал в `XamlNavigator.Platform`.

### Дополнительно для Stronghold (старая ветка WpfNavigator)

- Перевод: `WpfNavigator.Localization.L` → `XamlNavigator.Localization.NavigatorLocalization` (`L` оставлен устаревшим). **Ключи теперь английские**, а переводчик стоит первым: если `Translator` возвращает для незнакомого ключа сам ключ (как `L.T` Stronghold), встроенный русский перевод не сработает и строки навигатора останутся английскими. Возвращайте `null` для ключей, которых нет в своих таблицах, либо не задавайте `Translator` и используйте `NavigatorLocalization.Language` / `AddTranslations`. После смены языка без смены `CurrentUICulture` вызывайте `DynamicViewFactory.ResetFieldTemplates()`.
- `IDynamicPageRepository`: `DeleteRowAsync` теперь возвращает `Task<bool>` (`false` — запись удалить нельзя, страница покажет «Запись удалить нельзя…»), добавился `Type ResolveModelType(string typeName)` — он должен вернуть тип модели по `ReferenceModelTypeName` (например, из своего реестра или перебором `AppDomain.CurrentDomain.GetAssemblies()`); если вернуть `null`, поле-ссылка покажется простым полем.
- `NavigatorBuilder.OnNavigationFailed(nav => …)` и `IDynamicViewFactory.Register(id, factory)` без уровня доступа по-прежнему работают (перегрузка и метод-расширение).
- `AddDynamicNavigationItem` получил четвёртым параметром `requiredAccessLevel`. Позиционный вызов `AddDynamicNavigationItem(id, title, cmd, 2)` по-прежнему компилируется, но 2 теперь — уровень доступа, а не группа: передавайте группу, порядок и иконку по имени — `AddDynamicNavigationItem(id, title, cmd, cluster: 2, order: 1)`.
- `BaseWindowViewModel.NotValidCurrentView()` убран: окно без стартового экрана открывает первый доступный пункт меню.
- `NavigationItem.Tag` → `DynamicPageId` (`{ Tag: not null }` → `{ DynamicPageId: not null }`).
- Команды пунктов меню синхронные, как и были, — кнопки с индикатором выполнения для асинхронных команд на них не реагируют.

## MAUI (MauiNavigator)

1. Удалите проект `MauiNavigator`, подключите `XamlNavigator.Maui`.
2. Замените `MauiNavigator.` → `XamlNavigator.` в коде и XAML, затем исправьте `assembly=MauiNavigator` по таблице выше: `NavigatorPage` — `assembly=XamlNavigator.Maui`, `NavigationItem`/`NavigationCluster` (`x:DataType` в шаблонах меню) — `assembly=XamlNavigator`.
3. `CreateRootPage<T>()` и `OpenPageAsync<T>()` теперь методы-расширения: нужен `using XamlNavigator.Abstractions;` (обычно уже есть).
4. `ViewPair`, `NavigatorPage`, `UiThread`, `BasePageViewModel`, `BaseContentViewModel`, `HasParam`, `SetProperty`, `HistoryChanged`, `RelayCommand.ExecuteAsync` — на месте; `GetParam` → `TakeParam`, `ViewTypeModel` → `ViewTypePair`, `[ChildOf(Visible = …)]` → `ShowInMenu` (прежние имена — устаревшие).
5. Поменялось:
   - `BasePageViewModel.NotValidCurrentView()` убран: оболочка без стартового экрана открывает первый доступный пункт меню (как в WPF), без них — заглушку.
   - `BasePageViewModel.RestoreView()` → `SetCurrentViewAsync(view)`; `NavigatorBuilder.ViewTypeFor()` → `ViewLocator.ViewTypeFor()`.
   - `StaticViewFactory.GetPage<T>(orchestrator)` → `CreateShellView(typeof(T), orchestrator)`; `ViewBinder.CreateAndBindPage` → `CreateAndBindShellView`, `CreateAndBindContent` → `CreateAndBindContentView`; экземплярный `ViewBinder.Create(type, orchestrator)` → `CreateInstance(type, orchestrator)` (статический `Create` теперь принимает контейнер).
   - Команды пунктов меню синхронные (были асинхронными): `await item.Command.ExecuteAsync()` возвращается, как только переход запущен. Когда важно дождаться экрана, вызывайте переход напрямую — `await shell.SetCurrentViewAsync<T>()` или `await navigator.NavigateToAsync<T>()`. (`HistoryChanged` об окончании перехода по меню не сообщает: меню историю только очищает, и до перехода.)
   - `ViewPair.View` у текущего экрана — `object` (в XAML ничего менять не нужно).
   - `NavigationErrorEventArgs.FullMessage` — сообщение со всеми вложенными причинами.
   - Добавились права доступа (`UseAccessPolicy`) и динамические страницы.
6. В тестах без запущенного приложения привязки MAUI требуют диспетчер: поставьте `DispatcherProvider.SetCurrent(...)` с диспетчером, выполняющим всё на месте (см. `tests/XamlNavigator.Maui.Tests/TestDispatcher.cs`).
