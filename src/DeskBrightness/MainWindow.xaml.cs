using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using DeskBrightness.Config;
using DeskBrightness.ViewModels;

namespace DeskBrightness
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int SnackbarShowDurationMs = 250;
        private const int SnackbarHideDurationMs = 200;
        private const double SnackbarBottomShown = 24;
        private const double SnackbarBottomHidden = -100;
        private const string CanvasBottomProperty = "(Canvas.Bottom)";

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            SnackbarContent.Dismissed += (_, _) => viewModel.DismissInfoBar();

            Loaded += (_, _) =>
            {
                Snackbar.Width = ActualWidth - 40;
            };
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            var vm = (MainViewModel)DataContext;

            if (e.PropertyName == nameof(MainViewModel.IsInfoBarVisible))
            {
                AnimateSnackbar(vm.IsInfoBarVisible);
            }
            else if (e.PropertyName == nameof(MainViewModel.BrightnessValue))
            {
                AnimateSlider(vm.BrightnessValue);
            }
        }

        private void AnimateSlider(double newValue)
        {
            double current = BrightnessSlider.Value;
            double diff = Math.Abs(newValue - current);
            double durationMs = Math.Clamp(diff * 25, 150, 800);

            var animation = new DoubleAnimation
            {
                To = newValue,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new QuadraticEase(),
            };

            BrightnessSlider.BeginAnimation(Slider.ValueProperty, animation);
        }

        private void AnimateSnackbar(bool show)
        {
            if (show)
            {
                Snackbar.IsHitTestVisible = true;
                Snackbar.Opacity = 1;
                var sb = new Storyboard();
                var bottomAnim = new DoubleAnimation(SnackbarBottomHidden, SnackbarBottomShown, new Duration(TimeSpan.FromMilliseconds(SnackbarShowDurationMs)));
                Storyboard.SetTarget(bottomAnim, Snackbar);
                Storyboard.SetTargetProperty(bottomAnim, new PropertyPath(CanvasBottomProperty));
                sb.Children.Add(bottomAnim);
                sb.Begin();
            }
            else
            {
                Snackbar.IsHitTestVisible = false;
                Snackbar.Opacity = 0;
                var sb = new Storyboard();
                var bottomAnim = new DoubleAnimation(SnackbarBottomShown, SnackbarBottomHidden, new Duration(TimeSpan.FromMilliseconds(SnackbarHideDurationMs)));
                Storyboard.SetTarget(bottomAnim, Snackbar);
                Storyboard.SetTargetProperty(bottomAnim, new PropertyPath(CanvasBottomProperty));
                sb.Children.Add(bottomAnim);
                sb.Begin();
            }
        }

        private void AddDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element)
                return;

            if (element.ContextMenu is not ContextMenu menu)
                return;

            menu.PlacementTarget = element;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(AppConfig.Network.GitHubRepoUrl)
                {
                    UseShellExecute = true
                }
            );
        }

        private void LicenseButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(AppConfig.Network.GitHubLicenseUrl)
                {
                    UseShellExecute = true
                }
            );
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
