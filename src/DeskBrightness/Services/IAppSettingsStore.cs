namespace DeskBrightness.Services
{
    public interface IAppSettingsStore
    {
        AppSettings Load();

        void Save(AppSettings settings);
    }
}
