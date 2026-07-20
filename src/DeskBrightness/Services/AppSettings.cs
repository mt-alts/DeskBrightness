namespace DeskBrightness.Services
{
    public sealed class AppSettings
    {
        public string? LanguageCode { get; set; }

        public bool MinimizeToTray { get; set; }

        public bool CheckForUpdatesOnStartup { get; set; }

        public bool ShutdownAdbOnExit { get; set; } = true;
    }
}
