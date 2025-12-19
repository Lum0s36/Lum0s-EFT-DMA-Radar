/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Misc.JSON;

namespace LoneEftDmaRadar
{
    /// <summary>
    /// Widget configurations (Aimview, Info, Loot Info).
    /// </summary>
    public sealed class AimviewWidgetConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("minimized")]
        public bool Minimized { get; set; } = false;

        [JsonPropertyName("location")]
        [JsonConverter(typeof(SKRectJsonConverter))]
        public SKRect Location { get; set; }

        [JsonPropertyName("showLoot")]
        public bool ShowLoot { get; set; } = true;

        [JsonPropertyName("showQuestItems")]
        public bool ShowQuestItems { get; set; } = true;

        [JsonPropertyName("showAI")]
        public bool ShowAI { get; set; } = true;

        [JsonPropertyName("showEnemyPlayers")]
        public bool ShowEnemyPlayers { get; set; } = true;

        [JsonPropertyName("showHeadCircle")]
        public bool ShowHeadCircle { get; set; } = false;

        [JsonPropertyName("showWishlisted")]
        public bool ShowWishlisted { get; set; } = true;

        [JsonPropertyName("showContainers")]
        public bool ShowContainers { get; set; } = false;

        [JsonPropertyName("containerDistance")]
        public float ContainerDistance { get; set; } = 100f;

        [JsonPropertyName("showExfils")]
        public bool ShowExfils { get; set; } = true;
    }

    public sealed class InfoWidgetConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("minimized")]
        public bool Minimized { get; set; } = false;

        [JsonPropertyName("location")]
        [JsonConverter(typeof(SKRectJsonConverter))]
        public SKRect Location { get; set; }
    }

    public sealed class LootInfoWidgetConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonPropertyName("minimized")]
        public bool Minimized { get; set; } = false;

        [JsonPropertyName("location")]
        [JsonConverter(typeof(SKRectJsonConverter))]
        public SKRect Location { get; set; }
    }
}
