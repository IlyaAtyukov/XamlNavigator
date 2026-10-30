using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XamlNavigator.Abstractions;
using XamlNavigator.Core;
using XamlNavigator.DynamicPages;
using XamlNavigator.Models;
using XamlNavigator.Platform;

namespace XamlNavigator.Tests
{
    /// <summary>Представления-заглушки: оболочка и экран — обычные объекты с BindingContext.</summary>
    public abstract class FakeView
    {
        public object BindingContext { get; set; }
    }

    public abstract class FakeShellView : FakeView
    {
        public bool Shown { get; set; }
        public bool Closed { get; set; }
    }

    public abstract class FakeContentView : FakeView { }

    public sealed class FakeEmptyView : FakeContentView { }

    public sealed class FakeDynamicView : FakeContentView { }

    internal sealed class FakeViewAdapter : IViewAdapter
    {
        public bool IsShellView(Type viewType) => typeof(FakeShellView).IsAssignableFrom(viewType);
        public bool IsContentView(Type viewType) => typeof(FakeContentView).IsAssignableFrom(viewType);
        public bool IsDialogView(Type viewType) => typeof(FakeContentView).IsAssignableFrom(viewType);
        public object CreateView(Type viewType) => Activator.CreateInstance(viewType);
        public void Bind(object view, object viewModel) => ((FakeView)view).BindingContext = viewModel;
        public void AttachShell(object view, BaseShellViewModel viewModel, INavigationOrchestrator orchestrator) { }

        public void PresentShell(object view, object previousView)
        {
            ((FakeShellView)view).Shown = true;
            var previous = previousView as FakeShellView;
            if (previous != null) previous.Closed = true;
        }

        public void RollbackShell(object view, object previousView) { }
        public object CreateEmptyContentView(EmptyContentViewModel viewModel) => new FakeEmptyView();
    }

    internal sealed class FakeDynamicViewFactory : DynamicViewFactoryBase
    {
        protected override object BuildView(BaseContentViewModel viewModel) => new FakeDynamicView();
    }

    internal static class TestPlatform
    {
        private static readonly object Sync = new object();

        /// <summary>Платформа-заглушка и русская культура для текущего теста (вызывается в конструкторе класса тестов).</summary>
        public static void Install()
        {
            TestCulture.Russian();
            lock (Sync)
            {
                if (NavigatorPlatform.ViewAdapter is FakeViewAdapter) return;
                NavigatorPlatform.ViewAdapter = new FakeViewAdapter();
                NavigatorPlatform.DefaultDynamicViewFactory = () => new FakeDynamicViewFactory();
                NavigatorPlatform.MarkInstalled("Test");
            }
        }
    }

    internal sealed class Provider : INavigationServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public Provider Add<T>(T service)
        {
            _services[typeof(T)] = service;
            return this;
        }

        public object GetService(Type serviceType)
        {
            object service;
            return _services.TryGetValue(serviceType, out service) ? service : null;
        }

        public T GetService<T>() where T : class => GetService(typeof(T)) as T;
    }

    internal sealed class LevelPolicy : IAccessPolicy
    {
        public int Level { get; set; }
        public HashSet<Type> Denied { get; } = new HashSet<Type>();
        public bool CanAccess(Type viewModelType) => !Denied.Contains(viewModelType);
        public bool CanAccessLevel(int requiredLevel) => Level >= requiredLevel;
    }

    /// <summary>Репозиторий динамических страниц в памяти.</summary>
    internal sealed class MemoryRepository : IDynamicPageRepository
    {
        private readonly Dictionary<string, List<object>> _tables = new Dictionary<string, List<object>>();
        private readonly Dictionary<long, PageDefinitionModel> _pages = new Dictionary<long, PageDefinitionModel>();
        private long _nextId = 100;

        public List<object> Table(string name)
        {
            List<object> rows;
            if (!_tables.TryGetValue(name, out rows)) _tables[name] = rows = new List<object>();
            return rows;
        }

        public void AddPage(PageDefinitionModel page) => _pages[page.Id] = page;

        public Task<List<PageDefinitionModel>> GetPagesAsync() => Task.FromResult(_pages.Values.ToList());

        public Task<PageDefinitionModel> GetPageAsync(long pageId)
        {
            PageDefinitionModel page;
            return Task.FromResult(_pages.TryGetValue(pageId, out page) ? page : null);
        }

        public Task<long> InsertPageAsync(PageDefinitionModel page) => Task.FromResult(page.Id);
        public Task UpdatePageAsync(PageDefinitionModel page) => Task.CompletedTask;
        public Task DeletePageAsync(long pageId) => Task.CompletedTask;
        public Task EnsureTableAsync(string tableName, Type modelType) => Task.CompletedTask;

        public async Task<List<object>> GetRowsAsync(string tableName, Type modelType)
        {
            await Task.Yield();
            return Table(tableName).ToList();
        }

        public Task<long> InsertRowAsync(string tableName, Type modelType, object item)
        {
            var id = ++_nextId;
            modelType.GetProperty("Id").SetValue(item, id);
            Table(tableName).Add(item);
            return Task.FromResult(id);
        }

        public Task UpdateRowAsync(string tableName, Type modelType, object item)
        {
            var rows = Table(tableName);
            var id = (long)modelType.GetProperty("Id").GetValue(item);
            var index = rows.FindIndex(r => (long)modelType.GetProperty("Id").GetValue(r) == id);
            if (index >= 0) rows[index] = item;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteRowAsync(string tableName, long id)
        {
            var rows = Table(tableName);
            var removed = rows.RemoveAll(r => (long)r.GetType().GetProperty("Id").GetValue(r) == id);
            return Task.FromResult(removed > 0);
        }

        public Task<List<FieldDefinitionModel>> GetFieldsAsync(long pageId) => Task.FromResult(new List<FieldDefinitionModel>());
        public Task SaveFieldsAsync(long pageId, List<FieldDefinitionModel> defs) => Task.CompletedTask;
        public Task DropTableAsync(string tableName) => Task.CompletedTask;
        public Type ResolveModelType(string typeName) => typeof(MemoryRepository).Assembly.GetType(typeName);
    }
}
