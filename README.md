# XamlNavigator

[![CI](https://github.com/IlyaAtyukov/XamlNavigator/actions/workflows/ci.yml/badge.svg)](https://github.com/IlyaAtyukov/XamlNavigator/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/vpre/XamlNavigator.svg?label=XamlNavigator)](https://www.nuget.org/packages/XamlNavigator)
[![NuGet](https://img.shields.io/nuget/vpre/XamlNavigator.Wpf.svg?label=XamlNavigator.Wpf)](https://www.nuget.org/packages/XamlNavigator.Wpf)
[![NuGet](https://img.shields.io/nuget/vpre/XamlNavigator.Maui.svg?label=XamlNavigator.Maui)](https://www.nuget.org/packages/XamlNavigator.Maui)

MVVM-навигатор для **WPF** (.NET Framework 4.6.1+ и .NET 8+) и **.NET MAUI**: экраны связываются с моделями по имени, меню строится из атрибута `[ChildOf]`, есть история «назад», права доступа, диалоги и **динамические CRUD-страницы**, описанные данными (таблица, карточки, форма с проверками, поиск, печать, расчётные поля, фото). Модели экранов не зависят от UI — одну и ту же модель можно показать и в WPF, и в MAUI.

> *English: an MVVM navigator for WPF (.NET Framework 4.6.1+, .NET 8+) and .NET MAUI with convention-based view lookup, attribute-driven menus, back history, access policies, dialogs and data-driven CRUD pages. UI strings are English, with a built-in Russian translation that is picked automatically from the UI culture; add your own languages via `NavigatorLocalization.AddTranslations`.*

| Пакет | Что внутри | Цели |
|---|---|---|
| `XamlNavigator` | ядро без UI: модели, меню, история, права, динамические страницы (модели, расчёты, проверки) | net461, netstandard2.0, net8.0 |
| `XamlNavigator.Wpf` | Window/UserControl, диалоги, представления динамических страниц, печать | net461, net8.0-windows |
| `XamlNavigator.Maui` | Page/ContentView, `NavigatorPage` с аппаратной «Назад», модальные диалоги, представления динамических страниц | net10.0 (+ android, ios, maccatalyst, windows) |

Устанавливать нужно платформенный пакет — ядро придёт зависимостью.

```
dotnet add package XamlNavigator.Wpf --prerelease
dotnet add package XamlNavigator.Maui --prerelease
```

## Как это устроено

- **Оболочка** — окно WPF или страница MAUI, модель наследует `BaseShellViewModel` (или платформенные имена `BaseWindowViewModel` / `BasePageViewModel`). У оболочки есть меню (`NavigationClusters`) и текущий экран (`CurrentView`).
- **Экран** — UserControl или ContentView внутри оболочки, модель наследует `BaseContentViewModel` (в WPF можно `BaseUserControlViewModel`) и помечается `[ChildOf(typeof(ОболочкаViewModel))]`.
- **Связь по имени**: `MyApp.ViewModels.OrdersViewModel` ↔ `MyApp.Views.OrdersView`. Своё правило — `ViewLocator.Resolver`; представления в другой сборке — `NavigatorBuilder.UseViewsFrom(...)`.
- Зависимости моделей берутся из контейнера по самому длинному конструктору; `INavigationOrchestrator` подставляется любой модели; необязательные параметры получают значения по умолчанию.
- `InitializeAsync` модели выполняется один раз при первом появлении экрана; `OnAppearingAsync` / `OnDisappearingAsync` — при каждом показе и уходе.

```csharp
public class MainViewModel : BaseShellViewModel          // или BaseWindowViewModel / BasePageViewModel
{
    public MainViewModel() : base(startViewModelType: typeof(OrdersViewModel)) { }
}

[ChildOf(typeof(MainViewModel), Cluster = 0, Order = 1, DisplayName = "Заказы", IconKey = "📦")]
public class OrdersViewModel : BaseContentViewModel
{
    private readonly INavigationOrchestrator _navigator;

    public OrdersViewModel(INavigationOrchestrator navigator, IOrdersService orders)
    {
        _navigator = navigator;
        OpenCommand = new RelayCommand(o => _navigator.NavigateToAsync<OrderViewModel, Order>((Order)o));
    }

    public RelayCommand OpenCommand { get; }   // одна команда на модель, а не новая при каждом чтении свойства
}

[ChildOf(typeof(MainViewModel), ShowInMenu = false, DisplayName = "Заказ")]   // экран без пункта меню
public class OrderViewModel : BaseContentViewModel
{
    public override async Task OnAppearingAsync()
    {
        await base.OnAppearingAsync();
        if (HasParam<Order>()) Load(TakeParam<Order>());   // параметр забирается один раз
    }
}
```

## WPF

```csharp
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var services = new ServiceCollection()/* ... */.AddSingleton(_ => Navigator).BuildServiceProvider();

        Navigator = NavigatorBuilder
            .Create(new ServiceProviderAdapter(services))
            .ScanAssembly(typeof(App).Assembly)
            .UseAccessPolicy(new MyAccessPolicy())                        // необязательно
            .OnNavigationFailed((nav, error) => Log(error.FullMessage))
            .Build();

        _ = Navigator.OpenWindowAsync<MainViewModel>();                   // создаёт окно, делает главным, закрывает прежнее
    }

    public static INavigationOrchestrator Navigator { get; private set; }
}
```

```xml
<!-- MainView.xaml (Window) -->
<ItemsControl ItemsSource="{Binding NavigationClusters}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <ItemsControl ItemsSource="{Binding Items}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Button Command="{Binding Command}" Content="{Binding DisplayName}"/>   <!-- IsSelected, Icon -->
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
<ContentControl Content="{Binding CurrentView.View}"/>
```

Окно диалога (`DialogService` + `XxxDialogViewModel` ↔ `XxxDialogView`) имеет простое оформление по умолчанию; своё задаётся стилем `TargetType="{x:Type nav:DialogHostWindow}"` в ресурсах приложения (`xmlns:nav="https://github.com/IlyaAtyukov/XamlNavigator"`).

## .NET MAUI

```csharp
// MauiProgram.cs
builder.UseMauiApp<App>().UseXamlNavigator();

// App.xaml.cs
protected override Window CreateWindow(IActivationState? state) =>
    new Window(_navigator.CreateRootPage<MainViewModel>());
```

Оболочку лучше наследовать от `NavigatorPage`: аппаратная «Назад» на Android сначала листает историю экранов, и только когда она пуста, закрывает приложение. Переход по пункту меню в MAUI очищает историю (`NavigatorBuilder.ClearHistoryOnMenuNavigation(false)` — отключить). Смена оболочки — `OpenPageAsync<T>()`.

## Навигация

| | |
|---|---|
| `NavigateToAsync<T>()`, `NavigateToAsync<T, TParam>(p)` | экран текущей оболочки, с записью в историю |
| `NavigateBackAsync()`, `CanNavigateBack`, `HistoryChanged` | история «назад» |
| `NavigateParentToAsync<T>()` (из модели экрана), `ParentShell.SetCurrentViewAsync<T>()` | смена экрана своей оболочки без истории |
| `NavigateToDynamicAsync(pageId[, record])` | динамическая страница; с записью — открыть её на редактирование |
| `OpenAsync<T>()` / `OpenWindowAsync<T>()` / `OpenPageAsync<T>()` | другая оболочка (вход → главное окно) |
| `NavigationFailed` | ошибки переходов (`NavigateTo…`, «назад», `OpenAsync`, пункты меню, `NavigateParentToAsync`, обработчики появления окна/страницы) и отказы по правам — в интерфейс навигатор их не бросает. Исключение — `CreateRootView`/`CreateRootPage` при запуске: если стартовый экран не создаётся, без окна показывать нечего, и ошибка выходит наружу |

`IAccessPolicy.CanAccess(Type)` скрывает пункты меню и запрещает переходы, `CanAccessLevel(int)` — то же для динамических страниц. Если права поменялись (вошёл другой пользователь), вызовите у оболочки `RefreshAccess()` — меню пересоберётся.

Исключения асинхронных `RelayCommand` приложения команда не пробрасывает (async void уронил бы программу) — подпишитесь на `RelayCommand.ExecutionFailed`, чтобы их видеть.

## Динамические страницы

Страница описывается данными, а не кодом: `PageDefinitionModel` (заголовок, таблица, режим `Edit`/`List`, вид списка `Table`/`Cards`/`Custom`, парная страница, подсветка, разрешённые операции, уровень доступа) и список `FieldDefinitionModel` (тип поля, обязательность, проверки, справочник, расчёт). Данные читает и пишет ваш `IDynamicPageRepository` — обычно поверх базы.

```csharp
// при запуске: одна фабрика на приложение (DynamicViewFactory из XamlNavigator.Wpf или XamlNavigator.Maui)
var dynamicFactory = new DynamicViewFactory();
var navigator = NavigatorBuilder.Create(provider).ScanAssembly(appAssembly).UseDynamicViewFactory(dynamicFactory).Build();

// в оболочке (например, в InitializeAsync): страницы из базы — в фабрику и в меню
dynamicFactory.Register(page.Id, () => new DynamicPageViewModel(repository, navigator, page, fields, modelType, customView, bindings, accessPolicy), page.RequiredAccessLevel);
AddDynamicNavigationItem(page.Id, page.Title, new RelayCommand(_ => navigator.NavigateToDynamicAsync(page.Id)), page.RequiredAccessLevel);
```

- **Типы полей**: текст, многострочный, число, дата, да/нет, список значений, ссылка на другую таблицу, расчётное, фото.
- **Проверки**: обязательность, диапазон числа или длины, e-mail, телефон, только буквы/цифры, своё регулярное выражение (с тайм-аутом), своё сообщение.
- **Расчётные поля**: арифметика над полями и константами, агрегаты по связанной таблице (сумма, количество, среднее, минимум, максимум; за последние N дней), диапазоны («от–до → значение»). Пересчитываются на лету в форме; круговые ссылки и деление на ноль сообщаются, а не роняют страницу.
- **Список**: поиск по всем полям или по одному (понимает «1500» для «1 500»), сводка «Найдено: N из M», подсветка строк по условию, печать (WPF). Запись открывается на правку двойным щелчком по строке таблицы или своего представления, щелчком по карточке, тапом в MAUI; удаляется из контекстного меню (WPF, MAUI на Windows/Mac) или свайпом (MAUI).
- **Справочники** перечитываются при каждом показе страницы; навигационное свойство заполняется по имени ключа (`CategoryId` → `Category`).
- **Фото**: в базе хранится имя файла, файл — в папке `PhotoStore.FolderName` (по умолчанию «Photos») рядом с программой или в данных приложения MAUI; путь целиком — `PhotoStore.FolderPath`.

## Локализация

Строки библиотеки (кнопки, подсказки, сообщения проверки и ошибок) написаны по-английски. Русский перевод встроен и включается сам, если язык интерфейса (`CurrentUICulture`) русский. Порядок поиска: свой переводчик → встроенная таблица языка → английский текст.

```csharp
NavigatorLocalization.Language = "ru";                       // принудительно; null — по CurrentUICulture
NavigatorLocalization.AddTranslations("de", new Dictionary<string, string> { ["Save"] = "Speichern" /* … */ });
NavigatorLocalization.Translator = key => MyStrings.ResourceManager.GetString(key);   // null — встроенный перевод
```

Ключ — английский текст строки (полный список с русскими переводами — `src/XamlNavigator/Localization/RussianStrings.cs`). Уже показанные строки задним числом не переводятся; язык задают при запуске. WPF-форма пересобирает свои шаблоны сама при смене `Translator` или `CurrentUICulture`, иначе — `DynamicViewFactory.ResetFieldTemplates()`.

## Пример

`samples/` — одно приложение для WPF и MAUI с общими моделями экранов (`XamlNavigator.Samples.Shared`): главная, «О примере» и три динамические страницы над данными в памяти.

```
dotnet run --project samples/XamlNavigator.Samples.Wpf -f net8.0-windows
dotnet build samples/XamlNavigator.Samples.Maui -t:Run -f net10.0-windows10.0.19041.0
```

## Сборка и тесты

Решение целиком собирается только на Windows (WPF, тесты под .NET Framework 4.8). Нужны:
- .NET SDK 10+ и среда выполнения .NET 8 (Desktop) — на ней идут тесты и WPF-пример;
- .NET Framework 4.8 Developer Pack (или ссылочные сборки придут пакетом при сборке);
- рабочая нагрузка `maui` (`dotnet workload install maui`), для Android — Android SDK и JDK.

```
dotnet build XamlNavigator.sln -c Release
dotnet test XamlNavigator.sln -c Release
dotnet pack XamlNavigator.sln -c Release -o artifacts
```

Переход с прежних `WpfNavigator` / `MauiNavigator` — в [MIGRATION.md](https://github.com/IlyaAtyukov/XamlNavigator/blob/master/MIGRATION.md), изменения — в [CHANGELOG.md](https://github.com/IlyaAtyukov/XamlNavigator/blob/master/CHANGELOG.md).

## Лицензия

[MIT](https://github.com/IlyaAtyukov/XamlNavigator/blob/master/LICENSE)
