using XamlNavigator.Controls;

namespace XamlNavigator.Samples.Views
{
    /// <summary>
    /// Оболочка примера. На широком экране меню слева, на узком (телефон) — полосой под заголовком.
    /// </summary>
    public partial class MainView : NavigatorPage
    {
        private const double NarrowWidth = 720;
        private bool? _narrow;

        public MainView()
        {
            InitializeComponent();
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            if (width <= 0) return;

            var narrow = width < NarrowWidth;
            if (_narrow == narrow) return;
            _narrow = narrow;

            Root.ColumnDefinitions[0].Width = narrow ? new GridLength(0) : new GridLength(230);
            SideMenu.IsVisible = !narrow;
            StripMenu.IsVisible = narrow;
            Caption.IsVisible = !narrow;
            Grid.SetColumn(Host, narrow ? 0 : 1);
            Grid.SetColumnSpan(Host, narrow ? 2 : 1);
        }
    }
}
