/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

namespace LoneEftDmaRadar.UI.Skia
{
    /// <summary>
    /// Paint definitions for world elements (exfils, explosives, hazards) and UI rendering.
    /// </summary>
    internal static class SKWorldPaints
    {
        #region Exfils

        public static SKPaint PaintExfilOpen { get; } = new()
        {
            Color = SKColors.LimeGreen,
            StrokeWidth = SKConstants.LootStrokeWidth,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        public static SKPaint PaintExfilPending { get; } = new()
        {
            Color = SKColors.Yellow,
            StrokeWidth = SKConstants.LootStrokeWidth,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        public static SKPaint PaintExfilClosed { get; } = new()
        {
            Color = SKColors.Red.WithAlpha(SKConstants.ExfilClosedAlpha),
            StrokeWidth = SKConstants.LootStrokeWidth,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        public static SKPaint PaintExfilTransit { get; } = new()
        {
            Color = SKColors.Orange,
            StrokeWidth = SKConstants.LootStrokeWidth,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        public static SKPaint TextExfil { get; } = new()
        {
            Color = SKColors.White,
            IsStroke = false,
            IsAntialias = true,
        };

        #endregion

        #region Explosives & Hazards

        public static SKPaint PaintExplosives { get; } = new()
        {
            Color = SKColors.OrangeRed,
            StrokeWidth = SKConstants.ExplosiveStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
        };

        public static SKPaint PaintHazard { get; } = new()
        {
            Color = SKColors.OrangeRed,
            StrokeWidth = SKConstants.HazardStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
        };

        public static SKPaint TextHazard { get; } = new()
        {
            Color = SKColors.OrangeRed,
            IsStroke = false,
            IsAntialias = true,
        };

        #endregion

        #region UI Elements

        public static SKPaint PaintBitmap { get; } = new()
        {
            IsAntialias = true,
        };

        public static SKPaint PaintBitmapAlpha { get; } = new()
        {
            Color = SKColor.Empty.WithAlpha(SKConstants.BitmapAlphaValue),
            IsAntialias = true,
        };

        public static SKPaint PaintTransparentBacker { get; } = new()
        {
            Color = SKColors.Black.WithAlpha(SKConstants.TransparentBackerAlpha),
            StrokeWidth = SKConstants.LootStrokeWidth,
            Style = SKPaintStyle.Fill
        };

        public static SKPaint TextRadarStatus { get; } = new()
        {
            Color = SKColors.Red,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint TextStatusSmall { get; } = new SKPaint
        {
            Color = SKColors.Red,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint TextOutline { get; } = new()
        {
            IsAntialias = true,
            Color = SKColors.Black,
            IsStroke = true,
            StrokeWidth = SKConstants.TextOutlineStrokeWidth,
            Style = SKPaintStyle.Stroke,
        };

        /// <summary>
        /// Only utilize this paint on the Radar UI Thread. StrokeWidth is modified prior to each draw call.
        /// *NOT* Thread safe to use!
        /// </summary>
        public static SKPaint ShapeOutline { get; } = new()
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
        };

        #endregion
    }
}
