using System.Text.Json.Serialization;

namespace DeskBrightness.Models
{
    public sealed class ThirdPartyComponent
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("license")]
        public string License { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("licenseUrl")]
        public string LicenseUrl { get; set; } = string.Empty;
    }
}