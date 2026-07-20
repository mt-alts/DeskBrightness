using System;
using System.Windows;
using DeskBrightness.Adb;
using DeskBrightness.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DeskBrightness
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            var minSplash = Task.Delay(800);
            var adbTask = Task.Run(async () =>
            {
                var adb = ((App)Application.Current).Services.GetRequiredService<AdbCommandRunner>();
                return await adb.EnsureServerAsync();
            });

            await Task.WhenAll(minSplash, adbTask);

            Dispatcher.Invoke(() =>
            {
                var app = (App)Application.Current;
                var window = app.Services.GetRequiredService<MainWindow>();
                Application.Current.MainWindow = window;
                app.Services.GetRequiredService<SystemTrayService>().Attach(window);
                window.Closed += (_, _) => Application.Current.Shutdown();
                window.Show();
                Close();
            });
        }
    }
}
