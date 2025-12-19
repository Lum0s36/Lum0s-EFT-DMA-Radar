/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using Size = System.Windows.Size;

namespace LoneEftDmaRadar
{
    /// <summary>
    /// UI and Radar display configuration.
    /// </summary>
    public sealed class UIConfig
    {
        #region Window Settings

        [JsonPropertyName("scale")]
        public float UIScale { get; set; } = 1.0f;

        [JsonPropertyName("windowSize")]
        public Size WindowSize { get; set; } = new(1280, 720);

        [JsonPropertyName("resolution")]
        public Size Resolution { get; set; } = new(1920, 1080);

        [JsonPropertyName("windowMaximized")]
        public bool WindowMaximized { get; set; }

        [JsonPropertyName("zoom")]
        public int Zoom { get; set; } = 100;

        #endregion

        #region Player Display

        [JsonPropertyName("aimLineLength")]
        public int AimLineLength { get; set; } = 1500;

        [JsonPropertyName("fov")]
        public float FOV { get; set; } = 50.0f;

        [JsonPropertyName("hideNames")]
        public bool HideNames { get; set; }

        [JsonPropertyName("connectGroups")]
        public bool ConnectGroups { get; set; } = true;

        [JsonPropertyName("maxDistance")]
        public float MaxDistance { get; set; } = 350;

        [JsonPropertyName("teammateAimlines")]
        public bool TeammateAimlines { get; set; }

        [JsonPropertyName("teammateAimlineLength")]
        public int TeammateAimlineLength { get; set; } = 0;

        [JsonPropertyName("aiAimlines")]
        public bool AIAimlines { get; set; } = true;

        [JsonPropertyName("markSusPlayers")]
        public bool MarkSusPlayers { get; set; } = false;

        #endregion

        #region Radar Elements

        [JsonPropertyName("showMines")]
        public bool ShowMines { get; set; } = true;

        [JsonPropertyName("showHazards")]
        public bool ShowHazards { get; set; } = true;

        [JsonPropertyName("showExfils")]
        public bool ShowExfils { get; set; } = true;

        #endregion

        #region ESP Player Settings

        [JsonPropertyName("espPlayerSkeletons")]
        public bool EspPlayerSkeletons { get; set; } = true;

        [JsonPropertyName("espPlayerBoxes")]
        public bool EspPlayerBoxes { get; set; } = true;

        [JsonPropertyName("espAISkeletons")]
        public bool EspAISkeletons { get; set; } = true;

        [JsonPropertyName("espAIBoxes")]
        public bool EspAIBoxes { get; set; } = true;

        [JsonPropertyName("espPlayerNames")]
        public bool EspPlayerNames { get; set; } = true;

        [JsonPropertyName("espGroupIds")]
        public bool EspGroupIds { get; set; } = true;

        [JsonPropertyName("espPlayerFaction")]
        public bool EspPlayerFaction { get; set; } = false;

        [JsonPropertyName("espFactionColors")]
        public bool EspFactionColors { get; set; } = true;

        [JsonPropertyName("espGroupColors")]
        public bool EspGroupColors { get; set; } = true;

        [JsonPropertyName("espPlayerHealth")]
        public bool EspPlayerHealth { get; set; } = true;

        [JsonPropertyName("espPlayerDistance")]
        public bool EspPlayerDistance { get; set; } = true;

        [JsonPropertyName("espAINames")]
        public bool EspAINames { get; set; } = true;

        [JsonPropertyName("espAIGroupIds")]
        public bool EspAIGroupIds { get; set; } = false;

        [JsonPropertyName("espAIHealth")]
        public bool EspAIHealth { get; set; } = true;

        [JsonPropertyName("espAIDistance")]
        public bool EspAIDistance { get; set; } = true;

        [JsonPropertyName("espHeadCirclePlayers")]
        public bool EspHeadCirclePlayers { get; set; } = false;

        [JsonPropertyName("espHeadCircleAI")]
        public bool EspHeadCircleAI { get; set; } = false;

        #endregion

        #region ESP Display Settings

        [JsonPropertyName("showESP")]
        public bool ShowESP { get; set; } = true;

        [JsonPropertyName("espExfils")]
        public bool EspExfils { get; set; } = true;

        [JsonPropertyName("espTripwires")]
        public bool EspTripwires { get; set; } = true;

        [JsonPropertyName("espGrenades")]
        public bool EspGrenades { get; set; } = true;

        [JsonPropertyName("espLoot")]
        public bool EspLoot { get; set; } = true;

        [JsonPropertyName("espQuestLoot")]
        public bool EspQuestLoot { get; set; } = true;

        [JsonPropertyName("espLootPrice")]
        public bool EspLootPrice { get; set; } = true;

        [JsonPropertyName("espLootConeEnabled")]
        public bool EspLootConeEnabled { get; set; } = true;

        [JsonPropertyName("espLootConeAngle")]
        public float EspLootConeAngle { get; set; } = 15f;

        [JsonPropertyName("espFood")]
        public bool EspFood { get; set; } = false;

        [JsonPropertyName("espMeds")]
        public bool EspMeds { get; set; } = false;

        [JsonPropertyName("espBackpacks")]
        public bool EspBackpacks { get; set; } = false;

        [JsonPropertyName("espShowWishlisted")]
        public bool EspShowWishlisted { get; set; } = true;

        [JsonPropertyName("espLootFilterOnly")]
        public bool EspLootFilterOnly { get; set; } = false;

        [JsonPropertyName("espLootDebug")]
        public bool EspLootDebug { get; set; } = false;

        [JsonPropertyName("espCorpses")]
        public bool EspCorpses { get; set; } = false;

        [JsonPropertyName("espContainers")]
        public bool EspContainers { get; set; } = false;

        [JsonPropertyName("espNearestPlayerInfo")]
        public bool EspNearestPlayerInfo { get; set; } = true;

        [JsonPropertyName("espCrosshair")]
        public bool EspCrosshair { get; set; }

        [JsonPropertyName("espCrosshairLength")]
        public float EspCrosshairLength { get; set; } = 25f;

        #endregion

        #region ESP Font Settings

        [JsonPropertyName("espFontFamily")]
        public string EspFontFamily { get; set; } = "Segoe UI";

        [JsonPropertyName("espFontSizeSmall")]
        public int EspFontSizeSmall { get; set; } = 10;

        [JsonPropertyName("espFontSizeMedium")]
        public int EspFontSizeMedium { get; set; } = 12;

        [JsonPropertyName("espFontSizeLarge")]
        public int EspFontSizeLarge { get; set; } = 24;

        #endregion

        #region ESP Screen Settings

        [JsonPropertyName("espScreenWidth")]
        public int EspScreenWidth { get; set; } = 0;

        [JsonPropertyName("espScreenHeight")]
        public int EspScreenHeight { get; set; } = 0;

        [JsonPropertyName("espMaxFPS")]
        public int EspMaxFPS { get; set; } = 0;

        [JsonPropertyName("espTargetScreen")]
        public int EspTargetScreen { get; set; } = 0;

        [JsonPropertyName("radarMaxFPS")]
        public int RadarMaxFPS { get; set; } = 0;

        #endregion

        #region ESP Colors

        [JsonPropertyName("espColorPlayers")]
        public string EspColorPlayers { get; set; } = "#FFFFFFFF";

        [JsonPropertyName("espColorAI")]
        public string EspColorAI { get; set; } = "#FFFFA500";

        [JsonPropertyName("espColorPlayerScavs")]
        public string EspColorPlayerScavs { get; set; } = "#FFFFFFFF";

        [JsonPropertyName("espColorRaiders")]
        public string EspColorRaiders { get; set; } = "#FFFFC70F";

        [JsonPropertyName("espColorBosses")]
        public string EspColorBosses { get; set; } = "#FFFF00FF";

        [JsonPropertyName("espColorLoot")]
        public string EspColorLoot { get; set; } = "#FFD0D0D0";

        [JsonPropertyName("espColorContainers")]
        public string EspColorContainers { get; set; } = "#FFFFFFCC";

        [JsonPropertyName("espColorExfil")]
        public string EspColorExfil { get; set; } = "#FF7FFFD4";

        [JsonPropertyName("espColorTripwire")]
        public string EspColorTripwire { get; set; } = "#FFFF4500";

        [JsonPropertyName("espColorGrenade")]
        public string EspColorGrenade { get; set; } = "#FFFF5500";

        [JsonPropertyName("espColorCrosshair")]
        public string EspColorCrosshair { get; set; } = "#FFFFFFFF";

        [JsonPropertyName("espColorFactionBear")]
        public string EspColorFactionBear { get; set; } = "#FFFF0000";

        [JsonPropertyName("espColorFactionUsec")]
        public string EspColorFactionUsec { get; set; } = "#FF0000FF";

        #endregion

        #region ESP Distance Limits

        [JsonPropertyName("espPlayerMaxDistance")]
        public float EspPlayerMaxDistance { get; set; } = 0f;

        [JsonPropertyName("espAIMaxDistance")]
        public float EspAIMaxDistance { get; set; } = 0f;

        [JsonPropertyName("espLootMaxDistance")]
        public float EspLootMaxDistance { get; set; } = 0f;

        [JsonPropertyName("aimviewLootRenderDistance")]
        public float AimviewLootRenderDistance { get; set; } = 25f;

        [JsonPropertyName("aimviewLootRenderDistanceMax")]
        public bool AimviewLootRenderDistanceMax { get; set; } = false;

        #endregion

        #region ESP Label Position

        [JsonPropertyName("espLabelPosition")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EspLabelPosition EspLabelPosition { get; set; } = EspLabelPosition.Top;

        [JsonPropertyName("espLabelPositionAI")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EspLabelPosition EspLabelPositionAI { get; set; } = EspLabelPosition.Top;

        #endregion
    }

    public enum EspLabelPosition
    {
        Top,
        Bottom
    }
}
