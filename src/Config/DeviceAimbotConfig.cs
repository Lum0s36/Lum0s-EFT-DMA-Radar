/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Tarkov.Unity.Structures;

namespace LoneEftDmaRadar
{
    /// <summary>
    /// Device Aimbot (KMBox) configuration.
    /// </summary>
    public sealed class DeviceAimbotConfig
    {
        public bool Enabled { get; set; }
        public bool AutoConnect { get; set; }
        public string LastComPort { get; set; }

        // Debug
        public bool ShowDebug { get; set; } = true;

        /// <summary>
        /// Smoothing factor for DeviceAimbot device aim. 1 = instant, higher = slower/smoother.
        /// </summary>
        public float Smoothing { get; set; } = 1.0f;

        // Targeting
        public Bones TargetBone { get; set; } = Bones.HumanHead;
        public float FOV { get; set; } = 90f;
        public float MaxDistance { get; set; } = 300f;
        public TargetingMode Targeting { get; set; } = TargetingMode.ClosestToCrosshair;
        public bool EnablePrediction { get; set; } = true;

        // Target Filters
        public bool TargetPMC { get; set; } = true;
        public bool TargetPlayerScav { get; set; } = true;
        public bool TargetAIScav { get; set; } = true;
        public bool TargetBoss { get; set; } = true;
        public bool TargetRaider { get; set; } = true;

        // KMBox NET
        public bool UseKmBoxNet { get; set; } = false;
        public string KmBoxNetIp { get; set; } = "192.168.2.4";
        public int KmBoxNetPort { get; set; } = 8888;
        public string KmBoxNetMac { get; set; } = "";

        // FOV Circle Display
        public bool ShowFovCircle { get; set; } = true;
        public string FovCircleColorEngaged { get; set; } = "#FF00FF00";
        public string FovCircleColorIdle { get; set; } = "#80FFFFFF";

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum TargetingMode
        {
            ClosestToCrosshair,
            ClosestDistance
        }
    }

    /// <summary>
    /// Settings for memory write based features.
    /// </summary>
    public sealed class MemWritesConfig
    {
        public bool Enabled { get; set; }
        public bool NoRecoilEnabled { get; set; }
        public float NoRecoilAmount { get; set; } = 80f;
        public float NoSwayAmount { get; set; } = 80f;
        public bool InfiniteStaminaEnabled { get; set; }
        public bool MemoryAimEnabled { get; set; }
        public Bones MemoryAimTargetBone { get; set; } = Bones.HumanHead;
    }
}
