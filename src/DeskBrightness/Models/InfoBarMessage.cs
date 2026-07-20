using System;

namespace DeskBrightness.Models
{
    public sealed class InfoBarMessage
    {
        public InfoBarSeverity Severity { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public TimeSpan AutoCloseDelay { get; set; } = TimeSpan.FromSeconds(6);
    }
}