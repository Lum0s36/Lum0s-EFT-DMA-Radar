/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Misc.JSON;
using LoneEftDmaRadar.UI.Loot;

namespace LoneEftDmaRadar
{
    /// <summary>
    /// Loot display configuration.
    /// </summary>
    public sealed class LootConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("showQuestItems")]
        public bool ShowQuestItems { get; set; } = true;

        [JsonPropertyName("hideCorpses")]
        public bool HideCorpses { get; set; }

        [JsonPropertyName("showCorpseMarkers")]
        public bool ShowCorpseMarkers { get; set; } = false;

        [JsonPropertyName("minValue")]
        public int MinValue { get; set; } = 50000;

        [JsonPropertyName("minValueValuable")]
        public int MinValueValuable { get; set; } = 200000;

        [JsonPropertyName("pricePerSlot")]
        public bool PricePerSlot { get; set; }

        [JsonPropertyName("priceMode")]
        public LootPriceMode PriceMode { get; set; } = LootPriceMode.FleaMarket;

        [JsonPropertyName("showWishlistedRadar")]
        public bool ShowWishlistedRadar { get; set; } = true;

        [JsonPropertyName("wishlistColorRadar")]
        public string WishlistColorRadar { get; set; } = "#FFFF0000";
    }

    /// <summary>
    /// Static containers configuration.
    /// </summary>
    public sealed class ContainersConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonPropertyName("drawDistance")]
        public float DrawDistance { get; set; } = 100f;

        [JsonPropertyName("espDrawDistance")]
        public float EspDrawDistance { get; set; } = 100f;

        [JsonPropertyName("selectAll")]
        public bool SelectAll { get; set; } = true;

        [JsonPropertyName("hideSearched")]
        public bool HideSearched { get; set; } = false;

        [JsonPropertyName("selected_v4")]
        [JsonInclude]
        [JsonConverter(typeof(CaseInsensitiveConcurrentDictionaryConverter<byte>))]
        public ConcurrentDictionary<string, byte> Selected { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Loot filter configuration.
    /// </summary>
    public sealed class LootFilterConfig
    {
        [JsonPropertyName("selected")]
        public string Selected { get; set; } = "default";

        [JsonInclude]
        [JsonPropertyName("filters")]
        public ConcurrentDictionary<string, UserLootFilter> Filters { get; private set; } = new()
        {
            ["default"] = new()
        };
    }
}
