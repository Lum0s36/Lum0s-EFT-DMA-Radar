/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Common.DMA;

namespace LoneEftDmaRadar
{
    /// <summary>
    /// DMA hardware configuration.
    /// </summary>
    public sealed class DMAConfig
    {
        /// <summary>
        /// FPGA Read Algorithm
        /// </summary>
        [JsonPropertyName("fpgaAlgo")]
        public FpgaAlgo FpgaAlgo { get; set; } = FpgaAlgo.Auto;

        /// <summary>
        /// Use a Memory Map for FPGA DMA Connection.
        /// </summary>
        [JsonPropertyName("enableMemMap")]
        public bool MemMapEnabled { get; set; } = true;

        /// <summary>
        /// Force a full memory refresh when a raid ends to prevent stale pointer issues.
        /// </summary>
        [JsonPropertyName("autoRefreshOnRaidEnd")]
        public bool AutoRefreshOnRaidEnd { get; set; } = true;
    }
}
