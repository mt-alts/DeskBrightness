namespace DeskBrightness.Config;

public static class AppConfig
{
    public static class Metadata
    {
        public const string AppName = "DeskBrightness";
        public const string InstanceMutexName = "DeskBrightness-App-Instance";
        public const string AppTitle = AppName;
        public const string FallbackVersion = "1.0.0";
    }

    public static class Network
    {
        public const int TcpLuxPort = 8765;
        public const int AdbConnectDefaultPort = 5555;
        public const int AdbPairDefaultPort = 37099;
        public const string GitHubApiUrl =
            "https://api.github.com/repos/mt-alts/DeskBrightness/releases/latest";
        public const string GitHubRepoUrl = "https://github.com/mt-alts/DeskBrightness";
        public const string GitHubLicenseUrl =
            "https://github.com/mt-alts/DeskBrightness/blob/main/LICENSE";
        public const string HttpClientUserAgent = "DeskBrightness";
    }

    public static class Adb
    {
        public const string AdbFileName = "adb.exe";
        public const int SocketTimeoutMs = 5000;
        public const int ConnectTimeoutMs = 1500;
        public const string HeadlessRemotePath = "/data/local/tmp/deskbrightness-headless.jar";
        public const string HeadlessLogPath = "/data/local/tmp/deskbrightness-headless.log";
        public const string HeadlessInternalLogPath =
            "/data/local/tmp/deskbrightness-headless-internal.log";
        public const string HeadlessBridgeClass = "com.deskbrightness.mobile.HeadlessBridge";
        public const int HeadlessLogTailLines = 40;
        public const int HeadlessInternalLogTailLines = 80;
        public const int MaxTcpFrameLength = 65536;
    }

    public static class FileSystem
    {
        public const string SettingsFileName = "settings.json";
        public const string SettingsFolderName = "DeskBrightness";
        public const string LicensesFileName = "Resources/licenses.json";
        public const string IconFileName = "DeskBrightness.ico";
        public const string TempFolderName = "DeskBrightness";
        public const string DefaultSetupFileName = "DeskBrightness.Setup.exe";
        public const string UpdateArg = "--update /SILENT";
    }

    public static class Ui
    {
        public const double WindowWidth = 420;
        public const double WindowHeight = 780;
        public const double WindowMinWidth = 400;
        public const double WindowMinHeight = 740;
        public const int MaxLogLines = 200;
        public const int InfoBarErrorDurationSeconds = 15;
        public const int InfoBarInfoDurationSeconds = 6;
        public const int DownloadBufferSize = 8192;
    }

    public static class Brightness
    {
        public const byte DefaultMinimum = 0;
        public const byte DefaultMaximum = 100;
        public const double DefaultLuxThreshold = 2;
        public const byte DefaultStepThreshold = 4;
        public const int DefaultSmoothingWindow = 3;
        public const int DefaultSampleIntervalMs = 2000;
        public const int DefaultMinimumApplyIntervalSeconds = 3;
        public const double DefaultLowPassAlpha = 0.3;
        public const double DefaultLogCurveExponent = 0.75;
    }

    public static class Timing
    {
        public const int SmoothStepDelayMs = 25;
        public const int SmoothStepSmallChange = 3;
        public const int HeadlessWaitSeconds = 1;
        public const int KeepAliveTimerSeconds = 10;
    }
}
