using System;
using System.Linq;
using System.Threading.Tasks;
using XamlNavigator.Abstractions;
using XamlNavigator.Core;
using XamlNavigator.DynamicPages;
using XamlNavigator.Models;

namespace XamlNavigator.Samples.ViewModels
{
    /// <summary>
    /// Оболочка примера: окно в WPF, страница в MAUI. Статические экраны приходят из [ChildOf],
    /// динамические страницы — из репозитория, при первом появлении.
    /// </summary>
    public class MainViewModel : BaseShellViewModel
    {
        private readonly INavigationOrchestrator _navigator;
        private readonly IDynamicViewFactory _dynamicFactory;
        private readonly IDynamicPageRepository _repository;

        public MainViewModel(INavigationOrchestrator navigator, IDynamicViewFactory dynamicFactory, IDynamicPageRepository repository)
            : base(typeof(HomeViewModel))
        {
            _navigator = navigator;
            _dynamicFactory = dynamicFactory;
            _repository = repository;
            BackCommand = new RelayCommand(_ => _navigator.NavigateBackAsync(), _ => _navigator.CanNavigateBack);
            _navigator.HistoryChanged += (s, e) => BackCommand.RaiseCanExecuteChanged();
        }

        public override string HeaderCaption => "XamlNavigator";

        public RelayCommand BackCommand { get; }

        protected override async Task InitializeAsync()
        {
            var pages = await _repository.GetPagesAsync();
            foreach (var page in pages)
            {
                var fields = await _repository.GetFieldsAsync(page.Id);
                var modelType = _repository.ResolveModelType(page.ModelTypeName);
                var definition = page;
                _dynamicFactory.Register(page.Id, () => new DynamicPageViewModel(_repository, _navigator, definition, fields.ToList(), modelType, null, null), page.RequiredAccessLevel);

                if (page.DisplayMode == PageDisplayMode.List)
                    AddDynamicNavigationItem(page.Id, page.Title, new RelayCommand(_ => _navigator.NavigateToDynamicAsync(definition.Id)),
                        page.RequiredAccessLevel, cluster: 1, order: (int)page.Id, iconKey: IconFor(page));
            }
        }

        private static string IconFor(PageDefinitionModel page)
        {
            if (page.ListViewMode == ListViewMode.Cards) return "🛍";
            return page.TableName == "Categories" ? "🗂" : "📦";
        }
    }
}
