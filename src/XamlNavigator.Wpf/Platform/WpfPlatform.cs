using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.Win32;
using XamlNavigator.Abstractions;
using XamlNavigator.Core;
using XamlNavigator.DynamicPages;
using XamlNavigator.Localization;

namespace XamlNavigator.Platform
{
    /// <summary>
    /// Подключает WPF к ядру навигатора. Вызывается сам — из NavigatorBuilder.Build(), DialogService,
    /// DynamicViewFactory и базовых моделей WPF; явный вызов при запуске тоже допустим.
    /// </summary>
    public static class WpfPlatform
    {
        private static readonly object Sync = new object();
        private static bool _installed;

        public static void Install()
        {
            lock (Sync)
            {
                if (_installed) return;
                NavigatorPlatform.Dispatcher = new WpfUiDispatcher();
                NavigatorPlatform.CommandRequery = new WpfCommandRequery();
                NavigatorPlatform.RecordsViewFactory = new CollectionViewRecordsViewFactory();
                NavigatorPlatform.ViewAdapter = new WpfViewAdapter();
                NavigatorPlatform.Printer = new WpfReportPrinter();
                NavigatorPlatform.PhotoService = new WpfPhotoService();
                NavigatorPlatform.DefaultDynamicViewFactory = () => new DynamicViewFactory();
                NavigatorPlatform.ClearHistoryOnMenuNavigationByDefault = false;
                NavigatorPlatform.MarkInstalled("WPF");
                _installed = true;
            }
        }
    }

    /// <summary>
    /// Application.Current.Dispatcher. Без приложения (тесты, консоль) выполняет на месте.
    /// <see cref="InvokeAsync(Action)"/> всегда ставит в очередь, как Dispatcher.InvokeAsync.
    /// </summary>
    public sealed class WpfUiDispatcher : IUiDispatcher
    {
        private static System.Windows.Threading.Dispatcher Current => Application.Current?.Dispatcher;

        public bool CheckAccess()
        {
            var dispatcher = Current;
            return dispatcher == null || dispatcher.CheckAccess();
        }

        public void Invoke(Action action)
        {
            var dispatcher = Current;
            if (dispatcher == null || dispatcher.CheckAccess()) action();
            else dispatcher.Invoke(action);
        }

        public Task InvokeAsync(Action action)
        {
            var dispatcher = Current;
            if (dispatcher == null)
            {
                action();
                return Task.CompletedTask;
            }
            return dispatcher.InvokeAsync(action).Task;
        }

        public Task InvokeAsync(Func<Task> action)
        {
            var dispatcher = Current;
            if (dispatcher == null) return action();
            return dispatcher.InvokeAsync(action).Task.Unwrap();
        }
    }

    /// <summary>CommandManager.RequerySuggested: WPF сам перепроверяет команды после ввода.</summary>
    public sealed class WpfCommandRequery : ICommandRequery
    {
        public void Subscribe(RelayCommand command, EventHandler handler) => CommandManager.RequerySuggested += handler;

        public void Unsubscribe(RelayCommand command, EventHandler handler) => CommandManager.RequerySuggested -= handler;

        public void Invalidate(RelayCommand command) => InvalidateAll();

        public void InvalidateAll()
        {
            // Вызванный не из потока интерфейса InvalidateRequerySuggested уходит в диспетчер того потока и теряется.
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) CommandManager.InvalidateRequerySuggested();
            else dispatcher.BeginInvoke(new Action(CommandManager.InvalidateRequerySuggested));
        }
    }

    /// <summary>ICollectionView: фильтр без пересоздания списка, сортировка по столбцам DataGrid сохраняется.</summary>
    public sealed class CollectionViewRecordsViewFactory : IRecordsViewFactory
    {
        public IRecordsView Create(IList<object> records, Predicate<object> filter) => new CollectionViewRecordsView(records, filter);

        private sealed class CollectionViewRecordsView : IRecordsView
        {
            private readonly ICollectionView _view;

            public CollectionViewRecordsView(IList<object> records, Predicate<object> filter)
            {
                _view = CollectionViewSource.GetDefaultView(records);
                _view.Filter = filter;
            }

            public IEnumerable Source => _view;

            public void Refresh() => _view.Refresh();

            public IList<object> GetVisibleRecords() => _view.Cast<object>().ToList();
        }
    }

    public sealed class WpfViewAdapter : IViewAdapter
    {
        public bool IsShellView(Type viewType) => typeof(Window).IsAssignableFrom(viewType);

        public bool IsContentView(Type viewType) => typeof(UserControl).IsAssignableFrom(viewType);

        public bool IsDialogView(Type viewType) => typeof(UserControl).IsAssignableFrom(viewType);

        public object CreateView(Type viewType) => Activator.CreateInstance(viewType);

        public void Bind(object view, object viewModel)
        {
            var element = view as FrameworkElement;
            if (element == null) throw new InvalidOperationException(Tr.F("{0} is not a FrameworkElement.", view?.GetType().Name));
            element.DataContext = viewModel;
        }

        // Оркестратор окна — чтобы сообщить об ошибке там, где его не передают (закрытие прежнего окна).
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, INavigationOrchestrator> _orchestrators =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Window, INavigationOrchestrator>();

        public void AttachShell(object view, BaseShellViewModel viewModel, INavigationOrchestrator orchestrator)
        {
            var window = (Window)view;
            _orchestrators.Remove(window);
            _orchestrators.Add(window, orchestrator);
            // Обработчики async void: исключение из них уронило бы приложение — отдаём его в NavigationFailed.
            window.Loaded += async (sender, e) =>
            {
                try { await viewModel.OnShellAppearingAsync(); }
                catch (Exception ex) { Report(orchestrator, ex); }
            };
            // Closed, а не Closing: закрытие, отменённое приложением, не должно «уводить» экран.
            window.Closed += async (sender, e) =>
            {
                try { await viewModel.OnShellDisappearingAsync(); }
                catch (Exception ex) { Report(orchestrator, ex); }
            };
        }

        private static void Report(INavigationOrchestrator orchestrator, Exception exception)
        {
            var navigator = orchestrator as Services.NavigationOrchestrator;
            if (navigator != null) navigator.ReportFailure(exception);
        }

        public void PresentShell(object view, object previousView)
        {
            var window = (Window)view;
            if (Application.Current != null) Application.Current.MainWindow = window;
            window.Show();

            // Новое окно уже на экране — переход состоялся. Если прежнее не закрылось (его Closing бросил исключение),
            // откатывать нельзя: остались бы два окна с одним меню. Сообщаем об ошибке и идём дальше.
            var previous = previousView as Window;
            if (previous == null) return;
            try { previous.Close(); }
            catch (Exception ex)
            {
                INavigationOrchestrator orchestrator;
                if (_orchestrators.TryGetValue(window, out orchestrator)) Report(orchestrator, ex);
                else RelayCommand.ReportFailure(previous, ex);
            }
        }

        public void RollbackShell(object view, object previousView)
        {
            var previous = previousView as Window;
            if (previous != null && Application.Current != null) Application.Current.MainWindow = previous;
            var window = view as Window;
            if (window != null && !window.IsVisible) window.Close();
        }

        public object CreateEmptyContentView(EmptyContentViewModel viewModel) => EmptyContentView.Create(viewModel);
    }

    public sealed class WpfReportPrinter : IReportPrinter
    {
        public bool Print(Report report)
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true) return false;

            var document = ReportBuilder.Build(report.Title, report.Subtitle, report.Columns, report.Rows);
            document.PageWidth = dialog.PrintableAreaWidth;
            document.PageHeight = dialog.PrintableAreaHeight;
            document.ColumnWidth = dialog.PrintableAreaWidth;

            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, report.Title);
            return true;
        }
    }

    public sealed class WpfPhotoService : IPhotoService
    {
        public string DefaultFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PhotoStore.FolderName);

        public object LoadImage(string path, int height) => PhotoImageLoader.LoadFile(path, height);

        public void ClearImageCache() => PhotoImageLoader.ClearCache();

        public Task<string> PickImageAsync(string initialFolder)
        {
            var dialog = new OpenFileDialog
            {
                Title = Tr.T("Choose a photo"),
                InitialDirectory = initialFolder,
                Filter = Tr.T("Images") + " (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|" + Tr.T("All files") + " (*.*)|*.*"
            };

            return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
        }

        public bool CanOpenFolder => true;

        public void OpenFolder(string path) => Process.Start("explorer.exe", "\"" + path + "\"");
    }
}
