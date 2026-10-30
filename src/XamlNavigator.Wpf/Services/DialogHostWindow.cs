using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace XamlNavigator.Services
{
    public class DialogHostWindow : Window
    {
        static DialogHostWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DialogHostWindow), new FrameworkPropertyMetadata(typeof(DialogHostWindow)));
        }

        public DialogHostWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SizeToContent = SizeToContent.WidthAndHeight;
            Background = Brushes.Transparent;

            Opacity = 0;
            Loaded += OnLoadedAnimate;
            // Esc сначала закрывает раскрытый список или календарь внутри диалога, и только «свободный» Esc закрывает
            // сам диалог. ComboBox помечает Esc обработанным, а календарь DatePicker — нет: поэтому ещё на
            // PreviewKeyDown запоминаем, что нажатие пришло из всплывающего окна (у него свой PresentationSource).
            PreviewKeyDown += RememberPopupEscape;
            KeyDown += OnEscapeKeyDown;
        }

        private bool _escapeFromPopup;

        private void RememberPopupEscape(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            var origin = e.OriginalSource as Visual;
            _escapeFromPopup = origin != null && PresentationSource.FromVisual(origin) != PresentationSource.FromVisual(this);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            var dragArea = GetTemplateChild("PART_DragArea") as FrameworkElement;
            if (dragArea != null)
                dragArea.MouseLeftButtonDown += (sender, e) =>
                {
                    if (e.ChangedButton != MouseButton.Left) return;
                    try { DragMove(); } 
                    catch { /* ignored */ }
                };
        }

        private void OnEscapeKeyDown(object sender, KeyEventArgs e)
        {
            var fromPopup = _escapeFromPopup;
            _escapeFromPopup = false;
            if (e.Key != Key.Escape || e.Handled || fromPopup) return;
            DialogResult = false;
            Close();
            e.Handled = true;
        }

        private void OnLoadedAnimate(object sender, RoutedEventArgs e)
        {
            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, fade);

            var transform = new ScaleTransform(1, 1);
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = transform;

            var scale = new DoubleAnimation
            {
                From = 0.95,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        }
    }
}