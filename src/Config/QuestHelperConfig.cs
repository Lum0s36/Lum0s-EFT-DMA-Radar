/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Misc.JSON;

namespace LoneEftDmaRadar
{
    /// <summary>
    /// Quest Helper configuration.
    /// </summary>
    public sealed class QuestHelperConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("showLocations")]
        public bool ShowLocations { get; set; } = true;

        [JsonPropertyName("activeOnly")]
        public bool ActiveOnly { get; set; } = false;

        [JsonPropertyName("kappaOnly")]
        public bool KappaOnly { get; set; } = false;

        [JsonPropertyName("lightkeeperOnly")]
        public bool LightkeeperOnly { get; set; } = false;

        [JsonPropertyName("zoneDrawDistance")]
        public float ZoneDrawDistance { get; set; } = 100f;

        [JsonPropertyName("showWidget")]
        public bool ShowWidget { get; set; } = false;

        [JsonPropertyName("widgetMinimized")]
        public bool WidgetMinimized { get; set; } = false;

        [JsonPropertyName("widgetLocation")]
        [JsonConverter(typeof(SKRectJsonConverter))]
        public SKRect WidgetLocation { get; set; }

        [JsonPropertyName("trackedQuests")]
        [JsonInclude]
        public HashSet<string> TrackedQuests { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonPropertyName("blacklistedQuests")]
        [JsonInclude]
        public ConcurrentDictionary<string, byte> BlacklistedQuests { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
