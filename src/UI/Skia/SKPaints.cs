/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

namespace LoneEftDmaRadar.UI.Skia
{
    /// <summary>
    /// Facade class providing backward compatibility for paint access.
    /// Paints are organized into: SKPlayerPaints, SKLootPaints, SKWorldPaints, SKWidgetPaints
    /// </summary>
    internal static class SKPaints
    {
        /// <summary>
        /// Gets an SKColorFilter that will reduce an image's brightness level.
        /// </summary>
        public static SKColorFilter GetDarkModeColorFilter(float brightnessFactor)
        {
            float[] colorMatrix = {
                brightnessFactor, 0, 0, 0, 0,
                0, brightnessFactor, 0, 0, 0,
                0, 0, brightnessFactor, 0, 0,
                0, 0, 0, 1, 0,
            };
            return SKColorFilter.CreateColorMatrix(colorMatrix);
        }

        #region Player Paints (delegate to SKPlayerPaints)

        public static SKPaint PaintLocalPlayer => SKPlayerPaints.PaintLocalPlayer;
        public static SKPaint TextLocalPlayer => SKPlayerPaints.TextLocalPlayer;
        public static SKPaint PaintTeammate => SKPlayerPaints.PaintTeammate;
        public static SKPaint TextTeammate => SKPlayerPaints.TextTeammate;
        public static SKPaint PaintPMC => SKPlayerPaints.PaintPMC;
        public static SKPaint TextPMC => SKPlayerPaints.TextPMC;
        public static SKPaint PaintPMCBear => SKPlayerPaints.PaintPMCBear;
        public static SKPaint TextPMCBear => SKPlayerPaints.TextPMCBear;
        public static SKPaint PaintPMCUsec => SKPlayerPaints.PaintPMCUsec;
        public static SKPaint TextPMCUsec => SKPlayerPaints.TextPMCUsec;
        public static SKPaint PaintScav => SKPlayerPaints.PaintScav;
        public static SKPaint TextScav => SKPlayerPaints.TextScav;
        public static SKPaint PaintRaider => SKPlayerPaints.PaintRaider;
        public static SKPaint TextRaider => SKPlayerPaints.TextRaider;
        public static SKPaint PaintBoss => SKPlayerPaints.PaintBoss;
        public static SKPaint TextBoss => SKPlayerPaints.TextBoss;
        public static SKPaint PaintPScav => SKPlayerPaints.PaintPScav;
        public static SKPaint TextPScav => SKPlayerPaints.TextPScav;
        public static SKPaint PaintWatchlist => SKPlayerPaints.PaintWatchlist;
        public static SKPaint TextWatchlist => SKPlayerPaints.TextWatchlist;
        public static SKPaint PaintStreamer => SKPlayerPaints.PaintStreamer;
        public static SKPaint TextStreamer => SKPlayerPaints.TextStreamer;
        public static SKPaint PaintFocused => SKPlayerPaints.PaintFocused;
        public static SKPaint TextFocused => SKPlayerPaints.TextFocused;
        public static SKPaint PaintDeathMarker => SKPlayerPaints.PaintDeathMarker;
        public static SKPaint TextMouseover => SKPlayerPaints.TextMouseover;
        public static SKPaint PaintConnectorGroup => SKPlayerPaints.PaintConnectorGroup;
        public static SKPaint PaintMouseoverGroup => SKPlayerPaints.PaintMouseoverGroup;
        public static SKPaint TextMouseoverGroup => SKPlayerPaints.TextMouseoverGroup;

        #endregion

        #region Loot Paints (delegate to SKLootPaints)

        public static SKPaint PaintLoot => SKLootPaints.PaintLoot;
        public static SKPaint TextLoot => SKLootPaints.TextLoot;
        public static SKPaint PaintImportantLoot => SKLootPaints.PaintImportantLoot;
        public static SKPaint TextImportantLoot => SKLootPaints.TextImportantLoot;
        public static SKPaint PaintFilteredLoot => SKLootPaints.PaintFilteredLoot;
        public static SKPaint TextFilteredLoot => SKLootPaints.TextFilteredLoot;
        public static SKPaint PaintContainerLoot => SKLootPaints.PaintContainerLoot;
        public static SKPaint PaintCorpse => SKLootPaints.PaintCorpse;
        public static SKPaint TextCorpse => SKLootPaints.TextCorpse;
        public static SKPaint PaintMeds => SKLootPaints.PaintMeds;
        public static SKPaint TextMeds => SKLootPaints.TextMeds;
        public static SKPaint PaintFood => SKLootPaints.PaintFood;
        public static SKPaint TextFood => SKLootPaints.TextFood;
        public static SKPaint PaintBackpacks => SKLootPaints.PaintBackpacks;
        public static SKPaint TextBackpacks => SKLootPaints.TextBackpacks;
        public static SKPaint PaintQuestItem => SKLootPaints.PaintQuestItem;
        public static SKPaint TextQuestItem => SKLootPaints.TextQuestItem;
        public static SKPaint PaintWishlistItem => SKLootPaints.PaintWishlistItem;
        public static SKPaint TextWishlistItem => SKLootPaints.TextWishlistItem;
        public static SKPaint QuestHelperPaint => SKLootPaints.QuestHelperPaint;
        public static SKPaint QuestHelperText => SKLootPaints.QuestHelperText;
        public static SKPaint PaintQuestZone => SKLootPaints.PaintQuestZone;
        public static SKPaint TextQuestZone => SKLootPaints.TextQuestZone;

        #endregion

        #region World Paints (delegate to SKWorldPaints)

        public static SKPaint PaintExfilOpen => SKWorldPaints.PaintExfilOpen;
        public static SKPaint PaintExfilPending => SKWorldPaints.PaintExfilPending;
        public static SKPaint PaintExfilClosed => SKWorldPaints.PaintExfilClosed;
        public static SKPaint PaintExfilTransit => SKWorldPaints.PaintExfilTransit;
        public static SKPaint TextExfil => SKWorldPaints.TextExfil;
        public static SKPaint PaintExplosives => SKWorldPaints.PaintExplosives;
        public static SKPaint PaintHazard => SKWorldPaints.PaintHazard;
        public static SKPaint TextHazard => SKWorldPaints.TextHazard;
        public static SKPaint PaintBitmap => SKWorldPaints.PaintBitmap;
        public static SKPaint PaintBitmapAlpha => SKWorldPaints.PaintBitmapAlpha;
        public static SKPaint PaintTransparentBacker => SKWorldPaints.PaintTransparentBacker;
        public static SKPaint TextRadarStatus => SKWorldPaints.TextRadarStatus;
        public static SKPaint TextStatusSmall => SKWorldPaints.TextStatusSmall;
        public static SKPaint TextOutline => SKWorldPaints.TextOutline;
        public static SKPaint ShapeOutline => SKWorldPaints.ShapeOutline;

        #endregion

        #region Widget Paints (delegate to SKWidgetPaints)

        public static SKPaint TextPlayersOverlay => SKWidgetPaints.TextPlayersOverlay;
        public static SKPaint TextPlayersOverlayPMC => SKWidgetPaints.TextPlayersOverlayPMC;
        public static SKPaint TextPlayersOverlayPScav => SKWidgetPaints.TextPlayersOverlayPScav;
        public static SKPaint TextPlayersOverlayStreamer => SKWidgetPaints.TextPlayersOverlayStreamer;
        public static SKPaint TextPlayersOverlaySpecial => SKWidgetPaints.TextPlayersOverlaySpecial;
        public static SKPaint TextPlayersOverlayFocused => SKWidgetPaints.TextPlayersOverlayFocused;
        public static SKPaint PaintAimviewWidgetCrosshair => SKWidgetPaints.PaintAimviewWidgetCrosshair;
        public static SKPaint PaintAimviewWidgetLocalPlayer => SKWidgetPaints.PaintAimviewWidgetLocalPlayer;
        public static SKPaint PaintAimviewWidgetPMC => SKWidgetPaints.PaintAimviewWidgetPMC;
        public static SKPaint PaintAimviewWidgetWatchlist => SKWidgetPaints.PaintAimviewWidgetWatchlist;
        public static SKPaint PaintAimviewWidgetStreamer => SKWidgetPaints.PaintAimviewWidgetStreamer;
        public static SKPaint PaintAimviewWidgetTeammate => SKWidgetPaints.PaintAimviewWidgetTeammate;
        public static SKPaint PaintAimviewWidgetBoss => SKWidgetPaints.PaintAimviewWidgetBoss;
        public static SKPaint PaintAimviewWidgetScav => SKWidgetPaints.PaintAimviewWidgetScav;
        public static SKPaint PaintAimviewWidgetRaider => SKWidgetPaints.PaintAimviewWidgetRaider;
        public static SKPaint PaintAimviewWidgetPScav => SKWidgetPaints.PaintAimviewWidgetPScav;
        public static SKPaint PaintAimviewWidgetFocused => SKWidgetPaints.PaintAimviewWidgetFocused;

        #endregion
    }
}
