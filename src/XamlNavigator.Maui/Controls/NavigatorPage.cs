using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace XamlNavigator.Controls
{
    /// <summary>
    /// База для страниц-оболочек навигатора. Аппаратная «Назад» на Android сначала листает историю
    /// экранов внутри страницы и только потом закрывает приложение.
    /// </summary>
    public class NavigatorPage : ContentPage
    {
        internal Func<bool> CanGoBack { get; set; }
        internal Func<Task> GoBack { get; set; }

        protected override bool OnBackButtonPressed()
        {
            if (CanGoBack != null && CanGoBack() && GoBack != null)
            {
                _ = GoBack();
                return true;
            }
            return base.OnBackButtonPressed();
        }
    }
}
