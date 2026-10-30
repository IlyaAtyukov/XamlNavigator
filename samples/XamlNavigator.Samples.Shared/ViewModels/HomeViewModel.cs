using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using XamlNavigator.Abstractions;
using XamlNavigator.Attributes;
using XamlNavigator.Core;
using XamlNavigator.DynamicPages;
using XamlNavigator.Samples.Data;
using XamlNavigator.Samples.Models;

namespace XamlNavigator.Samples.ViewModels
{
    [ChildOf(typeof(MainViewModel), Order = 0, DisplayName = "Home", IconKey = "🏠")]
    public class HomeViewModel : BaseContentViewModel
    {
        private readonly INavigationOrchestrator _navigator;
        private readonly IDynamicPageRepository _repository;
        private string _summary = "";

        public HomeViewModel(INavigationOrchestrator navigator, IDynamicPageRepository repository)
        {
            _navigator = navigator;
            _repository = repository;

            OpenProductsCommand = new RelayCommand(_ => _navigator.NavigateToDynamicAsync(DemoRepository.ProductsTable));
            OpenShowcaseCommand = new RelayCommand(_ => _navigator.NavigateToDynamicAsync(DemoRepository.ProductsCards));
            AddProductCommand = new RelayCommand(_ => _navigator.NavigateToDynamicAsync(DemoRepository.ProductForm));
            AboutCommand = new RelayCommand(_ => _navigator.NavigateToAsync<AboutViewModel, string>("from the Home screen"));
        }

        public string Summary
        {
            get { return _summary; }
            private set { SetProperty(ref _summary, value); }
        }

        public RelayCommand OpenProductsCommand { get; }
        public RelayCommand OpenShowcaseCommand { get; }
        public RelayCommand AddProductCommand { get; }
        public RelayCommand AboutCommand { get; }

        /// <summary>Сводка пересчитывается при каждом появлении: данные могли поменяться на других страницах.</summary>
        public override async Task OnAppearingAsync()
        {
            await base.OnAppearingAsync();

            var products = (await _repository.GetRowsAsync("Products", typeof(Product))).OfType<Product>().ToList();
            var categories = await _repository.GetRowsAsync("Categories", typeof(Category));
            var total = products.Sum(p => p.Price * p.Quantity);

            Summary = string.Format(CultureInfo.CurrentCulture,
                "Categories: {0}, products: {1}, stock value: {2:N0} ₽. Out of stock: {3}.",
                categories.Count, products.Count, total, products.Count(p => p.Quantity == 0));
        }
    }
}
