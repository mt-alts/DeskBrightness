using System.Windows;
using DeskBrightness.Adb;
using DeskBrightness.Config;
using DeskBrightness.Services;
using DeskBrightness.ViewModels;
using DeskBrightness.Core.Brightness;
using DeskBrightness.Core.Profiles;
using DeskBrightness.Win;
using Microsoft.Extensions.DependencyInjection;

namespace DeskBrightness
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly System.Threading.Mutex _instanceMutex = new(true, AppConfig.Metadata.InstanceMutexName);
        private ServiceProvider? _services;

        internal ServiceProvider? Services => _services;

        protected override void OnStartup(StartupEventArgs e)
        {
            if (!_instanceMutex.WaitOne(TimeSpan.Zero, true))
            {
                var hWnd = AppNativeMethods.FindWindow(null, AppConfig.Metadata.AppTitle);
                if (hWnd != IntPtr.Zero)
                {
                    AppNativeMethods.ShowWindow(hWnd, 0x0001);
                    AppNativeMethods.ShowWindow(hWnd, 0x0005);
                    AppNativeMethods.SetForegroundWindow(hWnd);
                }
                Shutdown();
                return;
            }

            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();

            ConfigureServices(serviceCollection);

            _services = serviceCollection.BuildServiceProvider();

            var setupLang = ParseLanguageFromCommandLine();
            if (setupLang is not null)
            {
                var settingsStore = _services.GetRequiredService<IAppSettingsStore>();
                var settings = settingsStore.Load();
                settings.LanguageCode = setupLang;
                settingsStore.Save(settings);
            }

            _services.GetRequiredService<LocalizationService>().Initialize();

            new SplashWindow().Show();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_services is not null)
            {
                var runtime = _services.GetService<DeskBrightnessRuntime>();
                if (runtime is not null)
                    await runtime.StopAsync();

                var adb = _services.GetService<AdbCommandRunner>();
                if (adb is not null)
                {
                    var settings = _services.GetService<IAppSettingsStore>()?.Load();
                    if (settings is not null && !settings.ShutdownAdbOnExit)
                    {
                        await adb.ShutdownAsync(force: true);
                        adb.Dispose();
                    }
                    else
                    {
                        adb.SkipDisposeOnExit();
                    }
                }

                var trayService = _services.GetService<SystemTrayService>();
                trayService?.Dispose();

                await _services.DisposeAsync();
            }

            base.OnExit(e);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(
                new BrightnessProfile
                {
                    MinimumBrightness = AppConfig.Brightness.DefaultMinimum,
                    MaximumBrightness = AppConfig.Brightness.DefaultMaximum,
                    LuxThreshold = AppConfig.Brightness.DefaultLuxThreshold,
                    BrightnessStepThreshold = AppConfig.Brightness.DefaultStepThreshold,
                    SampleIntervalMs = AppConfig.Brightness.DefaultSampleIntervalMs,
                    SmoothingWindowSize = AppConfig.Brightness.DefaultSmoothingWindow,
                    MinimumApplyInterval = TimeSpan.FromSeconds(AppConfig.Brightness.DefaultMinimumApplyIntervalSeconds),
                    LowPassAlpha = AppConfig.Brightness.DefaultLowPassAlpha,
                    LogCurveExponent = AppConfig.Brightness.DefaultLogCurveExponent,
                }
            );

            services.AddSingleton<ILuxToBrightnessMapper, DefaultLuxToBrightnessMapper>();
            services.AddSingleton<BrightnessDecisionEngine>();

            services.AddSingleton(provider =>
            {
                var adbPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(
                        System.Reflection.Assembly.GetExecutingAssembly().Location
                    ) ?? ".",
                                        AppConfig.Adb.AdbFileName
                );
                var localization = provider.GetRequiredService<LocalizationService>();
                return new AdbCommandRunner(adbPath, key => localization.Get(key));
            });
            services.AddSingleton<AdbDeviceDetector>();

            services.AddSingleton<ForwardedLuxSource>();
            services.AddSingleton<AdbConnectDialogService>();
            services.AddSingleton<AdbPairDialogService>();
            services.AddSingleton<IAppSettingsStore, JsonAppSettingsStore>();
            services.AddSingleton<LocalizationService>();

            services.AddSingleton<MonitorDetector>();

            services.AddSingleton<IBrightnessController>(provider =>
            {
                var controllers = new List<IBrightnessController>();

                var wmi = new WmiBrightnessController();

                if (wmi.IsSupported())
                    controllers.Add(wmi);

                var ddc = new DdcCiBrightnessController(
                    provider.GetRequiredService<MonitorDetector>()
                );

                if (ddc.IsSupported())
                    controllers.Add(ddc);

                if (controllers.Count == 0)
                    throw new InvalidOperationException("No brightness controller is supported.");

                var composite = new CompositeBrightnessController(controllers);
                return new SmoothBrightnessController(composite);
            });

            services.AddSingleton<DeskBrightnessRuntime>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();
            services.AddSingleton<SystemTrayService>();
        }

        private static string? ParseLanguageFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--lang", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    var lang = args[i + 1].ToLowerInvariant();
                    return lang switch
                    {
                        "turkish" => "tr",
                        "english" => "en",
                        _ => null,
                    };
                }
            }
            return null;
        }
    }

    internal static class AppNativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        internal static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
