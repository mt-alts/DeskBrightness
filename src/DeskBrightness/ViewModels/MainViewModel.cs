using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using DeskBrightness.Adb;
using DeskBrightness.Config;
using DeskBrightness.Models;
using DeskBrightness.Services;
using DeskBrightness.Views;
using Windows.Services.Store;
using DeskBrightness.Core.Brightness;
using DeskBrightness.Core.Profiles;

namespace DeskBrightness.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private const int MaximumLogLines = AppConfig.Ui.MaxLogLines;
        private const string GitHubApiUrl = AppConfig.Network.GitHubApiUrl;
        private const string GitRepoUrl = AppConfig.Network.GitHubRepoUrl;
        private const string LogLevelInfo = "INFO";
        private const string LogLevelError = "ERROR";
        private const string LogLevelDebug = "DEBUG";

        private readonly DeskBrightnessRuntime _runtime;
        private readonly AdbDeviceDetector _deviceDetector;
        private readonly AdbCommandRunner _adbCommandRunner;
        private readonly AdbConnectDialogService _adbConnectDialogService;
        private readonly AdbPairDialogService _adbPairDialogService;
        private readonly LocalizationService _localization;
        private readonly ForwardedLuxSource _luxSource;
        private readonly BrightnessProfile _profile;
        private readonly BrightnessDecisionEngine _decisionEngine;
        private readonly IAppSettingsStore _settingsStore;
        private readonly List<string> _logs = new List<string>();

        private string _status = string.Empty;
        private string _rawLux = "-";
        private string _smoothedLux = "-";
        private string _brightness = "-";
        private double _brightnessValue;
        private string _logText = string.Empty;
        private bool _isRunning;
        private bool _isSettingsOpen;
        private bool _isHelpOpen;
        private string _latestVersion = string.Empty;
        private string _latestReleaseUrl = string.Empty;
        private bool _isCheckingUpdate;
        private bool _updateAvailable;
        private double _updateProgress;
        private string _updateProgressText = string.Empty;
        private string _pendingDownloadUrl = string.Empty;
        private string _pendingFileName = string.Empty;
        private long _pendingFileSize;
        private bool _minimizeToTray;
        private bool _checkForUpdatesOnStartup;
        private bool _shutdownAdbOnExit = true;
        private DeviceListItem? _selectedDevice;

        public MainViewModel(
            DeskBrightnessRuntime runtime,
            AdbDeviceDetector deviceDetector,
            AdbCommandRunner adbCommandRunner,
            AdbConnectDialogService adbConnectDialogService,
            AdbPairDialogService adbPairDialogService,
            LocalizationService localization,
            ForwardedLuxSource luxSource,
            BrightnessProfile profile,
            BrightnessDecisionEngine decisionEngine,
            IAppSettingsStore settingsStore
        )
        {
            _runtime = runtime;
            _deviceDetector = deviceDetector;
            _adbCommandRunner = adbCommandRunner;
            _adbConnectDialogService = adbConnectDialogService;
            _adbPairDialogService = adbPairDialogService;
            _localization = localization;
            _luxSource = luxSource;
            _profile = profile;
            _decisionEngine = decisionEngine;
            _settingsStore = settingsStore;
            _runtime.StateChanged += OnRuntimeStateChanged;
            _runtime.LogReceived += OnRuntimeLogReceived;

            var settings = _settingsStore.Load();
            _minimizeToTray = settings.MinimizeToTray;
            _checkForUpdatesOnStartup = settings.CheckForUpdatesOnStartup;
            _shutdownAdbOnExit = settings.ShutdownAdbOnExit;

            StartCommand = new RelayCommand(StartAsync, () => !IsRunning);
            StopCommand = new RelayCommand(StopAsync, () => IsRunning);
            DiscoverDevicesCommand = new RelayCommand(DiscoverDevicesAsync, () => !IsRunning);
            AddDeviceCommand = new RelayCommand(AddDeviceAsync, () => !IsRunning);
            PairDeviceCommand = new RelayCommand(PairDeviceAsync, () => !IsRunning);
            OpenSettingsCommand = new RelayCommand(OpenSettingsAsync);
            CloseSettingsCommand = new RelayCommand(CloseSettingsAsync);
            OpenHelpCommand = new RelayCommand(OpenHelpAsync);
            CloseHelpCommand = new RelayCommand(CloseHelpAsync);
            CheckForUpdatesCommand = new RelayCommand(CheckForUpdatesAsync, () => !IsCheckingUpdate);
            DownloadLatestCommand = new RelayCommand(DownloadLatestAsync);

            _ = DiscoverDevicesAsync();

            if (_checkForUpdatesOnStartup)
                _ = CheckForUpdatesOnStartupAsync();
            LoadThirdPartyComponents();
        }

        public string Status
        {
            get => _status;
            private set => SetField(ref _status, value);
        }

        public string RawLux
        {
            get => _rawLux;
            private set => SetField(ref _rawLux, value);
        }

        public string SmoothedLux
        {
            get => _smoothedLux;
            private set => SetField(ref _smoothedLux, value);
        }

        public string Brightness
        {
            get => _brightness;
            private set => SetField(ref _brightness, value);
        }

        public double BrightnessValue
        {
            get => _brightnessValue;
            private set => SetField(ref _brightnessValue, value);
        }

        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (SetField(ref _isRunning, value))
                {
                    ((RelayCommand)StartCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)StopCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DiscoverDevicesCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)AddDeviceCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)PairDeviceCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            private set => SetField(ref _isSettingsOpen, value);
        }

        public bool IsHelpOpen
        {
            get => _isHelpOpen;
            private set => SetField(ref _isHelpOpen, value);
        }

        public string AppVersion =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? AppConfig.Metadata.FallbackVersion;

        public string LatestVersion
        {
            get => _latestVersion;
            private set => SetField(ref _latestVersion, value);
        }

        public string LatestReleaseUrl
        {
            get => _latestReleaseUrl;
            private set => SetField(ref _latestReleaseUrl, value);
        }

        public bool IsCheckingUpdate
        {
            get => _isCheckingUpdate;
            private set
            {
                if (SetField(ref _isCheckingUpdate, value))
                {
                    ((RelayCommand)CheckForUpdatesCommand).RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(HasUpdateActivity));
                }
            }
        }

        public bool IsUpdateAvailable
        {
            get => _updateAvailable;
            private set
            {
                if (SetField(ref _updateAvailable, value))
                    OnPropertyChanged(nameof(HasUpdateActivity));
            }
        }

        public bool HasUpdateActivity => IsCheckingUpdate || IsUpdateAvailable;

        public double UpdateProgress
        {
            get => _updateProgress;
            private set => SetField(ref _updateProgress, value);
        }

        public string UpdateProgressText
        {
            get => _updateProgressText;
            private set => SetField(ref _updateProgressText, value);
        }

        public ObservableCollection<ThirdPartyComponent> ThirdPartyComponents { get; } = new();

        public int MinimumBrightnessSetting
        {
            get => _profile.MinimumBrightness;
            set
            {
                var clamped = ClampInt(value, 0, 100);

                if (clamped > _profile.MaximumBrightness)
                    _profile.MaximumBrightness = (byte)clamped;

                if (_profile.MinimumBrightness == clamped)
                    return;

                _profile.MinimumBrightness = (byte)clamped;
                ApplyProfileChange(nameof(MinimumBrightnessSetting));
                OnPropertyChanged(nameof(MaximumBrightnessSetting));
            }
        }

        public int MaximumBrightnessSetting
        {
            get => _profile.MaximumBrightness;
            set
            {
                var clamped = ClampInt(value, 0, 100);

                if (clamped < _profile.MinimumBrightness)
                    _profile.MinimumBrightness = (byte)clamped;

                if (_profile.MaximumBrightness == clamped)
                    return;

                _profile.MaximumBrightness = (byte)clamped;
                ApplyProfileChange(nameof(MaximumBrightnessSetting));
                OnPropertyChanged(nameof(MinimumBrightnessSetting));
            }
        }

        public double LuxThresholdSetting
        {
            get => _profile.LuxThreshold;
            set
            {
                var rounded = Math.Round(Math.Max(0, value), 1);

                if (Math.Abs(_profile.LuxThreshold - rounded) < 0.01)
                    return;

                _profile.LuxThreshold = rounded;
                ApplyProfileChange(nameof(LuxThresholdSetting));
            }
        }

        public int BrightnessStepThresholdSetting
        {
            get => _profile.BrightnessStepThreshold;
            set
            {
                var clamped = ClampInt(value, 0, 30);

                if (_profile.BrightnessStepThreshold == clamped)
                    return;

                _profile.BrightnessStepThreshold = (byte)clamped;
                ApplyProfileChange(nameof(BrightnessStepThresholdSetting));
            }
        }

        public int SmoothingWindowSizeSetting
        {
            get => _profile.SmoothingWindowSize;
            set
            {
                var clamped = ClampInt(value, 1, 20);

                if (_profile.SmoothingWindowSize == clamped)
                    return;

                _profile.SmoothingWindowSize = clamped;
                ApplyProfileChange(nameof(SmoothingWindowSizeSetting));
            }
        }

        public double MinimumApplyIntervalSecondsSetting
        {
            get => Math.Round(_profile.MinimumApplyInterval.TotalSeconds, 1);
            set
            {
                var clamped = Math.Round(Math.Max(0.5, Math.Min(30, value)), 1);

                if (Math.Abs(_profile.MinimumApplyInterval.TotalSeconds - clamped) < 0.01)
                    return;

                _profile.MinimumApplyInterval = TimeSpan.FromSeconds(clamped);
                ApplyProfileChange(nameof(MinimumApplyIntervalSecondsSetting));
            }
        }

        public bool MinimizeToTraySetting
        {
            get => _minimizeToTray;
            set
            {
                if (!SetField(ref _minimizeToTray, value))
                    return;

                var settings = _settingsStore.Load();
                settings.MinimizeToTray = value;
                _settingsStore.Save(settings);
            }
        }

        public bool CheckForUpdatesOnStartupSetting
        {
            get => _checkForUpdatesOnStartup;
            set
            {
                if (!SetField(ref _checkForUpdatesOnStartup, value))
                    return;

                var settings = _settingsStore.Load();
                settings.CheckForUpdatesOnStartup = value;
                _settingsStore.Save(settings);
            }
        }

        public bool ShutdownAdbOnExitSetting
        {
            get => _shutdownAdbOnExit;
            set
            {
                if (!SetField(ref _shutdownAdbOnExit, value))
                    return;

                var settings = _settingsStore.Load();
                settings.ShutdownAdbOnExit = value;
                _settingsStore.Save(settings);
            }
        }

        public ObservableCollection<DeviceListItem> Devices { get; } =
            new ObservableCollection<DeviceListItem>();

        public ObservableCollection<InfoBarMessage> InfoBarMessages { get; } = new();

        private InfoBarMessage? _currentInfoBarMessage;

        public InfoBarMessage? CurrentInfoBarMessage
        {
            get => _currentInfoBarMessage;
            private set
            {
                if (SetField(ref _currentInfoBarMessage, value))
                    OnPropertyChanged(nameof(IsInfoBarVisible));
            }
        }

        public bool IsInfoBarVisible => CurrentInfoBarMessage is not null;

        public void ShowInfoBar(InfoBarSeverity severity, string title, string message)
        {
            var msg = new InfoBarMessage
            {
                Severity = severity,
                Title = title,
                Message = message,
                Timestamp = DateTime.Now,
                AutoCloseDelay = severity == InfoBarSeverity.Error
                    ? TimeSpan.FromSeconds(AppConfig.Ui.InfoBarErrorDurationSeconds)
                    : TimeSpan.FromSeconds(AppConfig.Ui.InfoBarInfoDurationSeconds),
            };

            InfoBarMessages.Insert(0, msg);
            CurrentInfoBarMessage = msg;

            _ = AutoDismissInfoBarAsync(msg);
        }

        public void DismissInfoBar()
        {
            CurrentInfoBarMessage = null;
        }

        private async Task AutoDismissInfoBarAsync(InfoBarMessage msg)
        {
            try
            {
                await Task.Delay(msg.AutoCloseDelay);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (CurrentInfoBarMessage == msg)
                        CurrentInfoBarMessage = null;
                });
            }
            catch
            {
            }
        }

        public DeviceListItem? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (value is not null && !value.CanSelect)
                    value = null;

                if (SetField(ref _selectedDevice, value))
                    _luxSource.PreferredDeviceSerial = value?.Serial;
            }
        }

        public ICommand StartCommand { get; }

        public ICommand StopCommand { get; }

        public ICommand DiscoverDevicesCommand { get; }

        public ICommand AddDeviceCommand { get; }

        public ICommand PairDeviceCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand CloseSettingsCommand { get; }

        public ICommand OpenHelpCommand { get; }

        public ICommand CloseHelpCommand { get; }

        public ICommand CheckForUpdatesCommand { get; }

        public ICommand DownloadLatestCommand { get; }

        public IReadOnlyList<LanguageOption> AvailableLanguages => _localization.SupportedLanguages;

        public LanguageOption SelectedLanguage
        {
            get => AvailableLanguages.First(x =>
                string.Equals(x.CultureCode, _localization.CurrentCultureCode, StringComparison.OrdinalIgnoreCase)
            );
            set
            {
                if (value is null)
                    return;

                if (string.Equals(value.CultureCode, _localization.CurrentCultureCode, StringComparison.OrdinalIgnoreCase))
                    return;

                _localization.SetCulture(value.CultureCode);
                OnPropertyChanged(nameof(SelectedLanguage));
            }
        }

        public string LogText
        {
            get => _logText;
            private set => SetField(ref _logText, value);
        }

        private async Task StartAsync()
        {
            try
            {
                if (SelectedDevice is null)
                    await DiscoverDevicesAsync();

                AddLog(LogLevelInfo, _localization.Get("RuntimeStarted"));
                await _runtime.StartAsync();
                IsRunning = true;
            }
            catch (Exception ex)
            {
                Status = TranslateAdbError(ex.Message);
                AddLog(LogLevelError, ex.Message, showInfoBar: false);
                ShowInfoBar(Models.InfoBarSeverity.Error, _localization.Get("InfoBarTitleError"), Status);
                IsRunning = false;
            }
        }

        private async Task StopAsync()
        {
            try
            {
                await _runtime.StopAsync();
                IsRunning = false;
                AddLog(LogLevelInfo, _localization.Get("RuntimeStopped"));
            }
            catch (Exception ex)
            {
                Status = ex.Message;
                AddLog(LogLevelError, ex.Message);
            }
        }

        private async Task DiscoverDevicesAsync()
        {
            try
            {
                var previousSerial = SelectedDevice?.Serial;
                var devices = await _deviceDetector.GetDevicesAsync();

                Devices.Clear();

                foreach (var device in devices)
                {
                    string? warning = device.IsEmulator
                        ? _localization.Get("DeviceWarningEmulator")
                        : device.State == AdbDeviceState.Unauthorized
                            ? _localization.Get("DeviceWarningUnauthorized")
                            : device.State == AdbDeviceState.Offline
                                ? _localization.Get("DeviceWarningOffline")
                                : device.State != AdbDeviceState.Device
                                    ? string.Format(_localization.Get("DeviceWarningState"), device.State)
                                    : null;

                    Devices.Add(
                        new DeviceListItem(
                            device.Serial,
                            device.State,
                            device.ModelName,
                            device.IsEmulator,
                            warning
                        )
                    );
                }

                SelectedDevice =
                    Devices.FirstOrDefault(x => x.Serial == previousSerial && x.CanSelect)
                    ?? Devices.FirstOrDefault(x => x.CanSelect);

                AddLog(LogLevelInfo, string.Format(_localization.Get("DevicesFound"), Devices.Count));
            }
            catch (Exception ex)
            {
                Status = TranslateAdbError(ex.Message);
                AddLog(LogLevelError, ex.Message, showInfoBar: false);
                ShowInfoBar(Models.InfoBarSeverity.Error, _localization.Get("InfoBarTitleError"), Status);
            }
        }

        private async Task AddDeviceAsync()
        {
            var endpoint = _adbConnectDialogService.ShowDialog();

            if (endpoint is null)
            {
                return;
            }

            try
            {
                AddLog(LogLevelInfo, string.Format(_localization.Get("TcpIpConnecting"), endpoint.Serial));

                var result = await _adbCommandRunner.ConnectTcpAsync(endpoint.Host, endpoint.Port);

                if (result.ExitCode != 0)
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(result.StandardError)
                            ? $"adb connect failed. Exit code: {result.ExitCode}"
                            : result.StandardError.Trim()
                    );

                var output = result.StandardOutput.Trim();

                if (!string.IsNullOrWhiteSpace(output))
                    AddLog(LogLevelInfo, output);

                if (
                    output.IndexOf("unable", StringComparison.OrdinalIgnoreCase) >= 0
                    || output.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0
                    || output.IndexOf("cannot", StringComparison.OrdinalIgnoreCase) >= 0
                )
                    throw new InvalidOperationException(output);
            }
            catch (Exception ex)
            {
                Status = ex.Message;
                AddLog(LogLevelError, ex.Message, showInfoBar: false);
                ShowInfoBar(Models.InfoBarSeverity.Error, _localization.Get("InfoBarTitleError"), TranslateAdbError(ex.Message));
                return;
            }

            await DiscoverDevicesAsync();
        }

        private async Task PairDeviceAsync()
        {
            var endpoint = _adbPairDialogService.ShowDialog();

            if (endpoint is null)
            {
                return;
            }

            try
            {
                AddLog(LogLevelInfo, string.Format(_localization.Get("PairConnecting"), endpoint.Serial));

                var result = await _adbCommandRunner.PairTcpAsync(
                    endpoint.Host,
                    endpoint.Port,
                    endpoint.PairingCode
                );

                if (result.ExitCode != 0)
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(result.StandardError)
                            ? $"adb pair failed. Exit code: {result.ExitCode}"
                            : result.StandardError.Trim()
                    );

                var output = result.StandardOutput.Trim();

                if (!string.IsNullOrWhiteSpace(output))
                    AddLog(LogLevelInfo, output);

                if (
                    output.IndexOf("unable", StringComparison.OrdinalIgnoreCase) >= 0
                    || output.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0
                    || output.IndexOf("cannot", StringComparison.OrdinalIgnoreCase) >= 0
                )
                    throw new InvalidOperationException(output);
            }
            catch (Exception ex)
            {
                Status = ex.Message;
                AddLog(LogLevelError, ex.Message, showInfoBar: false);
                ShowInfoBar(Models.InfoBarSeverity.Error, _localization.Get("InfoBarTitleError"), TranslateAdbError(ex.Message));
                return;
            }

            await DiscoverDevicesAsync();
        }

        private string TranslateAdbError(string error)
        {
            if (error.Contains("protocol fault", StringComparison.OrdinalIgnoreCase))
                return _localization.Get("AdbErrorProtocolFault");
            if (error.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
                return _localization.Get("AdbErrorUnauthorized");
            if (error.Contains("connection refused", StringComparison.OrdinalIgnoreCase))
                return _localization.Get("AdbErrorConnectionRefused");
            if (error.Contains("cannot connect", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("failed to connect", StringComparison.OrdinalIgnoreCase))
                return _localization.Get("AdbErrorCannotConnect");
            if (error.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("timed out", StringComparison.OrdinalIgnoreCase))
                return _localization.Get("AdbErrorTimeout");
            if (error.Contains("no device", StringComparison.OrdinalIgnoreCase))
                return _localization.Get("AdbErrorNoDevice");
            if ((error.Contains("pairing code", StringComparison.OrdinalIgnoreCase) ||
                 error.Contains("authentication code", StringComparison.OrdinalIgnoreCase) ||
                 error.Contains("incorrect", StringComparison.OrdinalIgnoreCase) ||
                 error.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
                 error.Contains("wrong", StringComparison.OrdinalIgnoreCase)) &&
                error.Contains("code", StringComparison.OrdinalIgnoreCase))
                return _localization.Get("AdbErrorPairingCode");

            return _localization.Get("AdbErrorGeneric");
        }

        private Task OpenSettingsAsync()
        {
            IsSettingsOpen = true;
            return Task.CompletedTask;
        }

        private Task CloseSettingsAsync()
        {
            IsSettingsOpen = false;
            return Task.CompletedTask;
        }

        private Task OpenHelpAsync()
        {
            IsHelpOpen = true;
            return Task.CompletedTask;
        }

        private Task CloseHelpAsync()
        {
            IsHelpOpen = false;
            return Task.CompletedTask;
        }

        private void LoadThirdPartyComponents()
        {
            try
            {
                var path = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(
                        System.Reflection.Assembly.GetExecutingAssembly().Location
                    ) ?? ".",
                    AppConfig.FileSystem.LicensesFileName
                );

                var json = System.IO.File.ReadAllText(path);
                var items = System.Text.Json.JsonSerializer.Deserialize<List<ThirdPartyComponent>>(json);

                if (items is not null)
                {
                    ThirdPartyComponents.Clear();
                    foreach (var item in items)
                        ThirdPartyComponents.Add(item);
                }
            }
            catch
            {
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            IsCheckingUpdate = true;
            IsUpdateAvailable = false;
            LatestVersion = string.Empty;
            LatestReleaseUrl = string.Empty;

            try
            {
                if (TryDetectMsix())
                {
                    await CheckStoreUpdatesAsync();
                }
                else
                {
                    await CheckGitHubUpdatesAsync();
                }
            }
            catch (System.Exception ex)
            {
                AddLog(LogLevelError, string.Format(_localization.Get("UpdateCheckingFailed"), ex.Message));
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        }

        private async Task CheckForUpdatesOnStartupAsync()
        {
            await CheckForUpdatesAsync();

            if (!IsUpdateAvailable || string.IsNullOrWhiteSpace(LatestVersion))
                return;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var dialog = new UpdateDialog
                {
                    Owner = System.Windows.Application.Current.MainWindow,
                    DataContext = this,
                };

                if (dialog.ShowDialog() == true && dialog.DownloadConfirmed)
                {
                    if (TryDetectMsix())
                    {
                        var context = StoreContext.GetDefault();
                        var updates = await context.GetAppAndOptionalStorePackageUpdatesAsync();
                        if (updates.Count > 0)
                            await context.RequestDownloadAndInstallStorePackageUpdatesAsync(updates);
                    }
                    else
                    {
                        await ConfirmDownloadAsync();
                    }
                }
            });
        }

        private async Task CheckStoreUpdatesAsync()
        {
            UpdateProgressText = _localization.Get("UpdateStoreChecking");
            UpdateProgress = 0;

            var context = StoreContext.GetDefault();
            var updates = await context.GetAppAndOptionalStorePackageUpdatesAsync();

            if (updates.Count > 0)
            {
                UpdateProgressText = string.Format(_localization.Get("UpdateStoreDownloading"), updates.Count);
                UpdateProgress = 30;

                var result = await context.RequestDownloadAndInstallStorePackageUpdatesAsync(updates);
                UpdateProgress = 80;

                switch (result.OverallState)
                {
                    case StorePackageUpdateState.Completed:
                        UpdateProgressText = _localization.Get("UpdateStoreCompleted");
                        UpdateProgress = 100;
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                            () => System.Windows.Application.Current.Shutdown()
                        );
                        break;
                    case StorePackageUpdateState.Canceled:
                        UpdateProgressText = _localization.Get("UpdateStoreCanceled");
                        UpdateProgress = 0;
                        break;
                    default:
                        UpdateProgressText = string.Format(_localization.Get("UpdateStoreFailed"), result.OverallState);
                        UpdateProgress = 0;
                        break;
                }
            }
            else
            {
                UpdateProgressText = _localization.Get("UpdateUpToDate");
                UpdateProgress = 100;
            }
        }

        private static bool TryDetectMsix()
        {
            try
            {
                var sb = new System.Text.StringBuilder(256);
                int len = sb.Capacity;
                return NativeMethods.GetCurrentPackageFullName(ref len, sb) == 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task CheckGitHubUpdatesAsync()
        {
            UpdateProgressText = _localization.Get("UpdateGitHubChecking");
            UpdateProgress = 0;

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(AppConfig.Network.HttpClientUserAgent);

            var json = await client.GetStringAsync(
                GitHubApiUrl
            );

            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var htmlUrl = doc.RootElement.GetProperty("html_url").GetString() ?? "";

            var current = Assembly.GetExecutingAssembly().GetName().Version;
            var latestStr = tag.TrimStart('v');

            if (!Version.TryParse(latestStr, out var latest) || current is null)
                return;

            if (!(latest > current))
            {
                UpdateProgressText = _localization.Get("UpdateUpToDate");
                UpdateProgress = 100;
                AddLog(LogLevelInfo, _localization.Get("UpdateUpToDate"));
                return;
            }

            IsUpdateAvailable = true;
            LatestVersion = tag;
            LatestReleaseUrl = htmlUrl;
            UpdateProgressText = string.Format(_localization.Get("UpdateAvailable"), tag);
            AddLog(LogLevelInfo, string.Format(_localization.Get("UpdateAvailable"), tag));

            if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
            {
                JsonElement asset = assets[0];

                for (int i = 0; i < assets.GetArrayLength(); i++)
                {
                    var name = assets[i].GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        asset = assets[i];
                        break;
                    }
                }

                _pendingFileName = asset.GetProperty("name").GetString() ?? AppConfig.FileSystem.DefaultSetupFileName;
                _pendingDownloadUrl = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                _pendingFileSize = asset.GetProperty("size").GetInt64();
            }
        }

        public async Task ConfirmDownloadAsync()
        {
            if (string.IsNullOrWhiteSpace(_pendingDownloadUrl))
                return;

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(AppConfig.Network.HttpClientUserAgent);
            await DownloadWithProgressAsync(_pendingDownloadUrl, _pendingFileName, _pendingFileSize, client);
        }

        private async Task DownloadWithProgressAsync(string url, string fileName, long totalSize, HttpClient client)
        {
            UpdateProgressText = _localization.Get("UpdateDownloadingProgress");
            UpdateProgress = 5;

            var destPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                AppConfig.FileSystem.TempFolderName,
                fileName
            );

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destPath)!);

            long totalRead = 0;
            var buffer = new byte[AppConfig.Ui.DownloadBufferSize];

            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = System.IO.File.Create(destPath);

                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalSize > 0)
                    {
                        UpdateProgress = 5.0 + (double)totalRead / totalSize * 90.0;
                    }
                }
            }

            UpdateProgress = 100;
            UpdateProgressText = _localization.Get("UpdateStarting");

            if (destPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(destPath, AppConfig.FileSystem.UpdateArg)
                    {
                        UseShellExecute = false,
                        WorkingDirectory = System.IO.Path.GetTempPath(),
                    }
                );

                Environment.Exit(0);
            }
            else
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(destPath)
                    {
                        UseShellExecute = true
                    }
                );
            }
        }

        private Task DownloadLatestAsync()
        {
            _ = ConfirmDownloadAsync();
            return Task.CompletedTask;
        }

        private void OnRuntimeStateChanged(object? sender, DeskBrightnessRuntimeState state)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Brightness = state.Brightness is null ? "-" : $"{state.Brightness}%";

                if (state.Brightness is not null)
                    BrightnessValue = state.Brightness.Value;
            });
        }

        private void OnRuntimeLogReceived(object? sender, DeskBrightnessRuntimeLog log)
        {
            if (string.Equals(log.Level, LogLevelDebug, StringComparison.OrdinalIgnoreCase))
                return;

            App.Current.Dispatcher.Invoke(() => AddLog(log.Level, log.Message, log.Timestamp));
        }

        private void AddLog(string level, string message, DateTimeOffset? timestamp = null, bool showInfoBar = true)
        {
            if (string.Equals(level, LogLevelDebug, StringComparison.OrdinalIgnoreCase))
                return;

            _logs.Insert(0, $"{timestamp ?? DateTimeOffset.Now:HH:mm:ss}  [{level}] {message}");

            while (_logs.Count > MaximumLogLines)
                _logs.RemoveAt(_logs.Count - 1);

            LogText = string.Join(Environment.NewLine, _logs);

            if (!showInfoBar)
                return;

            if (string.Equals(level, LogLevelError, StringComparison.OrdinalIgnoreCase))
            {
                ShowInfoBar(Models.InfoBarSeverity.Error, _localization.Get("InfoBarTitleError"), message);
            }
            else if (string.Equals(level, LogLevelInfo, StringComparison.OrdinalIgnoreCase))
            {
                if (!message.Contains("bağlan", StringComparison.OrdinalIgnoreCase) &&
                    !message.Contains("Bağlantı", StringComparison.OrdinalIgnoreCase) &&
                    !message.Contains("connection", StringComparison.OrdinalIgnoreCase))
                    return;

                ShowInfoBar(Models.InfoBarSeverity.Informational, _localization.Get("InfoBarTitleInfo"), message);
            }
        }

        private void ApplyProfileChange(string propertyName)
        {
            try
            {
                _decisionEngine.Reconfigure();
                OnPropertyChanged(propertyName);
            }
            catch (Exception ex)
            {
                AddLog(LogLevelError, $"{_localization.Get("ProfileApplyFailed")}: {ex.Message}");
            }
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool SetField<T>(
            ref T field,
            T value,
            [CallerMemberName] string? propertyName = null
        )
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            return true;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class DeviceListItem
    {
        public DeviceListItem(
            string serial,
            AdbDeviceState state,
            string? modelName,
            bool isEmulator,
            string? warningText = null
        )
        {
            Serial = serial;
            State = state;
            ModelName = modelName;
            IsEmulator = isEmulator;
            WarningText = warningText;
        }

        public string Serial { get; }

        public AdbDeviceState State { get; }

        public string? ModelName { get; }

        public bool IsEmulator { get; }

        public bool CanSelect => State == AdbDeviceState.Device && !IsEmulator;

        public string? WarningText { get; }

        public string DisplayName
        {
            get
            {
                if (IsEmulator)
                    return $"{FormatSerialForDisplay(Serial)} ({WarningText})";

                if (State != AdbDeviceState.Device)
                    return $"{Serial} ({State})";

                if (string.IsNullOrWhiteSpace(ModelName))
                    return Serial;

                return ModelName;
            }
        }

        private static string FormatSerialForDisplay(string serial)
        {
            const string emulatorPrefix = "emulator-";

            if (serial.StartsWith(emulatorPrefix, StringComparison.OrdinalIgnoreCase))
                return $"emulator:{serial.Substring(emulatorPrefix.Length)}";

            return serial;
        }
    }

    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        internal static extern int GetCurrentPackageFullName(ref int packageFullNameLength, System.Text.StringBuilder packageFullName);
    }
}
