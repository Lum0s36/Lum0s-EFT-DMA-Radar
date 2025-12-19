/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

namespace LoneEftDmaRadar.UI.Skia
{
    /// <summary>
    /// Paint definitions for widget overlays (Player Info Widget, Aimview Widget).
    /// </summary>
    internal static class SKWidgetPaints
    {
        #region Player Info Widget

        public static SKPaint TextPlayersOverlay { get; } = new()
        {
            Color = SKColors.White,
            IsStroke = false,
            IsAntialias = true
        };

        public static SKPaint TextPlayersOverlayPMC { get; } = new()
        {
            IsStroke = false,
            IsAntialias = true
        };

        public static SKPaint TextPlayersOverlayPScav { get; } = new()
        {
            IsStroke = false,
            IsAntialias = true
        };

        public static SKPaint TextPlayersOverlayStreamer { get; } = new()
        {
            IsStroke = false,
            IsAntialias = true
        };

        public static SKPaint TextPlayersOverlaySpecial { get; } = new()
        {
            IsStroke = false,
            IsAntialias = true
        };

        public static SKPaint TextPlayersOverlayFocused { get; } = new()
        {
            IsStroke = false,
            IsAntialias = true
        };

        #endregion

        #region Aimview Widget

        public static SKPaint PaintAimviewWidgetCrosshair { get; } = new()
        {
            Color = SKColors.White,
            StrokeWidth = SKConstants.ESPStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        public static SKPaint PaintAimviewWidgetLocalPlayer { get; } = new()
        {
            Color = SKColors.Green,
            StrokeWidth = SKConstants.ESPStrokeWidth,
            Style = SKPaintStyle.Stroke
        };

        public static SKPaint PaintAimviewWidgetPMC { get; } = new()
        {
            Color = SKColors.Red,
            StrokeWidth = SKConstants.ESPStrokeWidth,
            Style = SKPaintStyle.Stroke
        };

        public static SKPaint PaintAimviewWidgetWatchlist { get; } = new()
        {
            Color = SKColors.HotPink,
            StrokeWidth = SKConstants.ESPStrokeWidth,
            Style = SKPaintStyle.Stroke
        };

        public static SKPaint PaintAimviewWidgetStreamer { get; } = new()
        {
            Color = SKColors.MediumPurple,
            StrokeWidth = SKConstants.ESPStrokeWidth,
            Style = SKPaintStyle.Stroke
        };

        public static SKPaint PaintAimviewWidgetTeammate { get; } = new()
        {
            Color = SKColors.LimeGreen,
            StrokeWidth = SKConstants.ESPStrokeWidth,
            Style = SKPaintStyle.Stroke
        };

        public static SKPaint PaintAimviewWidgetBoss { get; } = new()
        {
            Color = SKColors.Fuchsia,
            StrokeWidth = SKConstants.ESPStrokeWidth,
            Style = SKPaintStyle.Stroke
        };

        public static SKPaint PaintAimviewWidgetScav { get; } = new()
        {
            Color = SKColors.Yellow,
            StrokeWidth = SKConstants.ESPStrokeWidth,
            Style = SKPaintStyle.Stroke
        };

        public static SKPaint PaintAimviewWidgetRaider { get; } = new()
        {
            Color = SKColor.Parse(SKConstants.RaiderColorHex),
            StrokeWidth = SKConstants.ESPStrokeWidth,
            Style = SKPaintStyle.Stroke
        };

        public static SKPaint PaintAimviewWidgetPScav { get; } = new()
        {
            Color = SKColors.White,
            StrokeWidth = SKConstants.ESPStrokeWidth,
            Style = SKPaintStyle.Stroke
        };

        public static SKPaint PaintAimviewWidgetFocused { get; } = new()
        {
            Color = SKColors.Coral,
            StrokeWidth = SKConstants.ESPStrokeWidth,
            Style = SKPaintStyle.Stroke
        };

        #endregion
    }
}
