using DeskBrightness.Config;
using System.IO;
using System.Text.Json;

namespace DeskBrightness.Services
{
    public sealed class JsonAppSettingsStore : IAppSettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
        };

        private readonly string _filePath;

        public JsonAppSettingsStore()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, AppConfig.FileSystem.SettingsFolderName);
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, AppConfig.FileSystem.SettingsFileName);
        }

        public AppSettings Load()
        {
            if (!File.Exists(_filePath))
                return new AppSettings();

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
    }
}
