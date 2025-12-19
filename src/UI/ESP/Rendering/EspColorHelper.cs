/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.UI.Skia;
using SkiaSharp;
using DxColor = SharpDX.Mathematics.Interop.RawColorBGRA;

namespace LoneEftDmaRadar.UI.ESP.Rendering
{
    /// <summary>
    /// Utility class for ESP color handling and conversions.
    /// </summary>
    internal static class EspColorHelper
    {
        #region Color Conversion

        /// <summary>
        /// Converts an SKPaint to DxColor.
        /// </summary>
        public static DxColor ToColor(SKPaint paint) => ToColor(paint.Color);

        /// <summary>
        /// Converts an SKColor to DxColor.
        /// </summary>
        public static DxColor ToColor(SKColor color) => new(color.Blue, color.Green, color.Red, color.Alpha);

        /// <summary>
        /// Parses a hex color string to SKColor.
        /// </summary>
        /// <param name="hex">Hex color string (e.g., "#FFFFFFFF" or "#FFFFFF").</param>
        /// <returns>Parsed SKColor, or White if parsing fails.</returns>
        public static SKColor ColorFromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return SKColors.White;

            try
            {
                return SKColor.Parse(hex);
            }
            catch
            {
                return SKColors.White;
            }
        }

        #endregion

        #region Config-Based Colors

        /// <summary>
        /// Gets the ESP color for players.
        /// </summary>
        public static DxColor GetPlayerColor() => ToColor(ColorFromHex(App.Config.UI.EspColorPlayers));

        /// <summary>
        /// Gets the ESP color for AI/Scavs.
        /// </summary>
        public static DxColor GetAIColor() => ToColor(ColorFromHex(App.Config.UI.EspColorAI));

        /// <summary>
        /// Gets the ESP color for player scavs.
        /// </summary>
        public static DxColor GetPlayerScavColor() => ToColor(ColorFromHex(App.Config.UI.EspColorPlayerScavs));

        /// <summary>
        /// Gets the ESP color for raiders.
        /// </summary>
        public static DxColor GetRaiderColor() => ToColor(ColorFromHex(App.Config.UI.EspColorRaiders));

        /// <summary>
        /// Gets the ESP color for bosses.
        /// </summary>
        public static DxColor GetBossColor() => ToColor(ColorFromHex(App.Config.UI.EspColorBosses));

        /// <summary>
        /// Gets the ESP color for loot.
        /// </summary>
        public static DxColor GetLootColor() => ToColor(ColorFromHex(App.Config.UI.EspColorLoot));

        /// <summary>
        /// Gets the ESP color for containers.
        /// </summary>
        public static DxColor GetContainerColor() => ToColor(ColorFromHex(App.Config.UI.EspColorContainers));

        /// <summary>
        /// Gets the ESP color for exfils.
        /// </summary>
        public static DxColor GetExfilColor() => ToColor(ColorFromHex(App.Config.UI.EspColorExfil));

        /// <summary>
        /// Gets the ESP color for tripwires.
        /// </summary>
        public static DxColor GetTripwireColor() => ToColor(ColorFromHex(App.Config.UI.EspColorTripwire));

        /// <summary>
        /// Gets the ESP color for grenades.
        /// </summary>
        public static DxColor GetGrenadeColor() => ToColor(ColorFromHex(App.Config.UI.EspColorGrenade));

        /// <summary>
        /// Gets the ESP color for crosshair.
        /// </summary>
        public static DxColor GetCrosshairColor() => ToColor(ColorFromHex(App.Config.UI.EspColorCrosshair));

        /// <summary>
        /// Gets the ESP color for BEAR faction.
        /// </summary>
        public static DxColor GetBearColor() => ToColor(ColorFromHex(App.Config.UI.EspColorFactionBear));

        /// <summary>
        /// Gets the ESP color for USEC faction.
        /// </summary>
        public static DxColor GetUsecColor() => ToColor(ColorFromHex(App.Config.UI.EspColorFactionUsec));

        #endregion

        #region SKPaint Colors (for shared rendering)

        /// <summary>
        /// Gets the color for quest items.
        /// </summary>
        public static DxColor GetQuestItemColor() => ToColor(SKPaints.PaintQuestItem);

        /// <summary>
        /// Gets the color for wishlist items.
        /// </summary>
        public static DxColor GetWishlistItemColor() => ToColor(SKPaints.PaintWishlistItem);

        /// <summary>
        /// Gets the color for important loot.
        /// </summary>
        public static DxColor GetImportantLootColor() => ToColor(SKPaints.PaintImportantLoot);

        /// <summary>
        /// Gets the color for backpacks.
        /// </summary>
        public static DxColor GetBackpackColor() => ToColor(SKPaints.PaintBackpacks);

        /// <summary>
        /// Gets the color for meds.
        /// </summary>
        public static DxColor GetMedsColor() => ToColor(SKPaints.PaintMeds);

        /// <summary>
        /// Gets the color for food.
        /// </summary>
        public static DxColor GetFoodColor() => ToColor(SKPaints.PaintFood);

        /// <summary>
        /// Gets the color for corpses.
        /// </summary>
        public static DxColor GetCorpseColor() => ToColor(SKPaints.PaintCorpse);

        /// <summary>
        /// Gets the color for open exfils.
        /// </summary>
        public static DxColor GetExfilOpenColor() => ToColor(SKPaints.PaintExfilOpen);

        /// <summary>
        /// Gets the color for pending exfils.
        /// </summary>
        public static DxColor GetExfilPendingColor() => ToColor(SKPaints.PaintExfilPending);

        /// <summary>
        /// Gets the color for transit points.
        /// </summary>
        public static DxColor GetTransitColor() => ToColor(SKPaints.PaintExfilTransit);

        #endregion

        #region Static Colors

        /// <summary>
        /// White color.
        /// </summary>
        public static DxColor White => new(255, 255, 255, 255);

        /// <summary>
        /// Semi-transparent black for backgrounds.
        /// </summary>
        public static DxColor BackgroundBlack(byte alpha = 180) => new(0, 0, 0, alpha);

        /// <summary>
        /// DeviceAimbot locked target color.
        /// </summary>
        public static DxColor DeviceAimbotLockedColor => ToColor(new SKColor(0, 200, 255, 220));

        /// <summary>
        /// DeviceAimbot engaged color.
        /// </summary>
        public static DxColor DeviceAimbotEngagedColor => ToColor(new SKColor(0, 200, 255, 200));

        /// <summary>
        /// DeviceAimbot idle color.
        /// </summary>
        public static DxColor DeviceAimbotIdleColor => ToColor(new SKColor(255, 210, 0, 180));

        #endregion
    }
}
