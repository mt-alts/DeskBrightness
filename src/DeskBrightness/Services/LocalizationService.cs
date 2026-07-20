using System.Globalization;
using System.Windows;

namespace DeskBrightness.Services
{
    public sealed class LocalizationService
    {
        private const string EnglishCulture = "en-US";
        private const string TurkishCulture = "tr-TR";
        private readonly IAppSettingsStore _settingsStore;
        private ResourceDictionary? _currentDictionary;
        private AppSettings _settings = new();

        public LocalizationService(IAppSettingsStore settingsStore)
        {
            _settingsStore = settingsStore;
        }

        public string CurrentCultureCode { get; private set; } = EnglishCulture;

        public IReadOnlyList<LanguageOption> SupportedLanguages { get; } =
        [
            new LanguageOption(EnglishCulture, "English"),
            new LanguageOption(TurkishCulture, "Türkçe"),
        ];

        public void Initialize()
        {
            _settings = _settingsStore.Load();

            var culture = ResolveInitialCulture(_settings.LanguageCode);
            ApplyCulture(culture, persist: true);
        }

        public void SetCulture(string cultureCode)
        {
            var culture = ResolveSupportedCulture(cultureCode);
            ApplyCulture(culture, persist: true);
        }

        public string Get(string key)
        {
            if (Application.Current.TryFindResource(key) is string value)
                return value;

            return key;
        }

        private void ApplyCulture(string cultureCode, bool persist)
        {
            var culture = CultureInfo.GetCultureInfo(cultureCode);

            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            if (_currentDictionary is not null)
                Application.Current.Resources.MergedDictionaries.Remove(_currentDictionary);

            _currentDictionary = new ResourceDictionary
            {
                Source = new Uri(
                    $"pack://application:,,,/DeskBrightness;component/Resources/Localization/Strings.{cultureCode}.xaml",
                    UriKind.Absolute
                ),
            };

            Application.Current.Resources.MergedDictionaries.Add(_currentDictionary);

            CurrentCultureCode = cultureCode;

            if (persist)
            {
                _settings.LanguageCode = cultureCode;
                _settingsStore.Save(_settings);
            }
        }

        private static string ResolveInitialCulture(string? savedCulture)
        {
            var cultureCode = ResolveSupportedCulture(savedCulture);

            if (!string.IsNullOrWhiteSpace(savedCulture))
                return cultureCode;

            var systemCulture = CultureInfo.CurrentUICulture.Name;
            return ResolveSupportedCulture(systemCulture);
        }

        private static string ResolveSupportedCulture(string? cultureCode)
        {
            if (string.IsNullOrWhiteSpace(cultureCode))
                return EnglishCulture;

            var normalized = cultureCode.Trim();

            if (IsSupported(normalized))
                return NormalizeCulture(normalized);

            var twoLetter = normalized.Length >= 2 ? normalized[..2].ToLowerInvariant() : normalized.ToLowerInvariant();

            return twoLetter switch
            {
                "tr" => TurkishCulture,
                _ => EnglishCulture,
            };
        }

        private static bool IsSupported(string cultureCode)
        {
            return string.Equals(NormalizeCulture(cultureCode), EnglishCulture, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizeCulture(cultureCode), TurkishCulture, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeCulture(string cultureCode)
        {
            return cultureCode.Trim().ToLowerInvariant() switch
            {
                "en" => EnglishCulture,
                "en-us" => EnglishCulture,
                "tr" => TurkishCulture,
                "tr-tr" => TurkishCulture,
                _ => cultureCode,
            };
        }
    }

    public sealed record LanguageOption(string CultureCode, string DisplayName);
}
