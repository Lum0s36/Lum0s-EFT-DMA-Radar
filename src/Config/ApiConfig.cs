/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

namespace LoneEftDmaRadar
{
    /// <summary>
    /// API configuration classes.
    /// </summary>
    public sealed class ProfileApiConfig
    {
        [JsonPropertyName("tarkovDev")]
        [JsonInclude]
        public TarkovDevConfig TarkovDev { get; private set; } = new();

        [JsonPropertyName("eftApiTech")]
        [JsonInclude]
        public EftApiTechConfig EftApiTech { get; private set; } = new();
    }

    public sealed class TwitchApiConfig
    {
        [JsonPropertyName("clientId")]
        public string ClientId { get; set; } = null;

        [JsonPropertyName("clientSecret")]
        public string ClientSecret { get; set; } = null;
    }

    public sealed class TarkovDevConfig
    {
        [JsonPropertyName("priority_v2")]
        public uint Priority { get; set; } = 1000;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;
    }

    public sealed class EftApiTechConfig
    {
        [JsonPropertyName("priority")]
        public uint Priority { get; set; } = 10;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonPropertyName("requestsPerMinute")]
        public int RequestsPerMinute { get; set; } = 5;

        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = null;
    }

    /// <summary>
    /// Web Radar configuration.
    /// </summary>
    public sealed class WebRadarConfig
    {
        [JsonPropertyName("upnp")]
        public bool UPnP { get; set; } = true;

        [JsonPropertyName("host")]
        public string IP { get; set; } = "0.0.0.0";

        [JsonPropertyName("port")]
        public string Port { get; set; } = Random.Shared.Next(50000, 60000).ToString();

        [JsonPropertyName("tickRate")]
        public string TickRate { get; set; } = "60";
    }

    /// <summary>
    /// Hotkey input mode.
    /// </summary>
    public enum HotkeyInputMode
    {
        RadarPC,
        GamePC
    }
}
