using System.Windows;
using System.Windows.Controls;

namespace XamlNavigator.Core
{
    /// <summary>
    /// Заглушка «страниц пока нет». Цвета берутся из ресурсов приложения AccentColor и SurfaceAltColor,
    /// если они есть.
    /// </summary>
    internal static class EmptyContentView
    {
        public static UserControl Create(EmptyContentViewModel viewModel)
        {
            var heading = new TextBlock
            {
                Text = viewModel.DisplayName,
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14)
            };
            heading.SetResourceReference(TextBlock.ForegroundProperty, "AccentColor");

            var body = new TextBlock
            {
                Text = viewModel.Message,
                FontSize = 14,
                LineHeight = 23,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 520
            };
            body.SetResourceReference(TextBlock.ForegroundProperty, "AccentColor");

            var stack = new StackPanel();
            stack.Children.Add(heading);
            stack.Children.Add(body);

            var card = new Border
            {
                Padding = new Thickness(36, 28, 36, 32),
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = stack
            };
            card.SetResourceReference(Border.BorderBrushProperty, "AccentColor");
            card.SetResourceReference(Border.BackgroundProperty, "SurfaceAltColor");

            return new UserControl
            {
                Content = card,
                Margin = new Thickness(20)
            };
        }
    }
}
