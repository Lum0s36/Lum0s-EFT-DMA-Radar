/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

namespace LoneEftDmaRadar.UI.Skia
{
    /// <summary>
    /// Paint definitions for player rendering on radar.
    /// </summary>
    internal static class SKPlayerPaints
    {
        #region Local Player & Teammates

        public static SKPaint PaintLocalPlayer { get; } = new()
        {
            Color = SKColors.Green,
            StrokeWidth = SKConstants.PlayerStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };

        public static SKPaint TextLocalPlayer { get; } = new()
        {
            Color = SKColors.Green,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint PaintTeammate { get; } = new()
        {
            Color = SKColors.LimeGreen,
            StrokeWidth = SKConstants.PlayerStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };

        public static SKPaint TextTeammate { get; } = new()
        {
            Color = SKColors.LimeGreen,
            IsStroke = false,
            IsAntialias = true,
        };

        #endregion

        #region PMC

        public static SKPaint PaintPMC { get; } = new()
        {
            Color = SKColors.Red,
            StrokeWidth = SKConstants.PlayerStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };

        public static SKPaint TextPMC { get; } = new()
        {
            Color = SKColors.Red,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint PaintPMCBear { get; } = new()
        {
            Color = SKColors.Red,
            StrokeWidth = SKConstants.PlayerStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };

        public static SKPaint TextPMCBear { get; } = new()
        {
            Color = SKColors.Red,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint PaintPMCUsec { get; } = new()
        {
            Color = SKColors.Blue,
            StrokeWidth = SKConstants.PlayerStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };

        public static SKPaint TextPMCUsec { get; } = new()
        {
            Color = SKColors.Blue,
            IsStroke = false,
            IsAntialias = true,
        };

        #endregion

        #region AI Players

        public static SKPaint PaintScav { get; } = new()
        {
            Color = SKColors.Yellow,
            StrokeWidth = SKConstants.PlayerStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };

        public static SKPaint TextScav { get; } = new()
        {
            Color = SKColors.Yellow,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint PaintRaider { get; } = new()
        {
            Color = SKColor.Parse(SKConstants.RaiderColorHex),
            StrokeWidth = SKConstants.PlayerStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };

        public static SKPaint TextRaider { get; } = new()
        {
            Color = SKColor.Parse(SKConstants.RaiderColorHex),
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint PaintBoss { get; } = new()
        {
            Color = SKColors.Fuchsia,
            StrokeWidth = SKConstants.PlayerStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };

        public static SKPaint TextBoss { get; } = new()
        {
            Color = SKColors.Fuchsia,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint PaintPScav { get; } = new()
        {
            Color = SKColors.White,
            StrokeWidth = SKConstants.PlayerStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };

        public static SKPaint TextPScav { get; } = new()
        {
            Color = SKColors.White,
            IsStroke = false,
            IsAntialias = true,
        };

        #endregion

        #region Special Players

        public static SKPaint PaintWatchlist { get; } = new()
        {
            Color = SKColors.HotPink,
            StrokeWidth = SKConstants.PlayerStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };

        public static SKPaint TextWatchlist { get; } = new()
        {
            Color = SKColors.HotPink,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint PaintStreamer { get; } = new()
        {
            Color = SKColors.MediumPurple,
            StrokeWidth = SKConstants.PlayerStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };

        public static SKPaint TextStreamer { get; } = new()
        {
            Color = SKColors.MediumPurple,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint PaintFocused { get; } = new()
        {
            Color = SKColors.Coral,
            StrokeWidth = SKConstants.PlayerStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };

        public static SKPaint TextFocused { get; } = new()
        {
            Color = SKColors.Coral,
            IsStroke = false,
            IsAntialias = true,
        };

        #endregion

        #region Other

        public static SKPaint PaintDeathMarker { get; } = new()
        {
            Color = SKColors.Black,
            StrokeWidth = SKConstants.DeathMarkerStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
        };

        public static SKPaint TextMouseover { get; } = new()
        {
            Color = SKColors.White,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint PaintConnectorGroup { get; } = new()
        {
            Color = SKColors.LawnGreen.WithAlpha(SKConstants.ConnectorGroupAlpha),
            StrokeWidth = SKConstants.ConnectorStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
        };

        public static SKPaint PaintMouseoverGroup { get; } = new()
        {
            Color = SKColors.LawnGreen,
            StrokeWidth = SKConstants.PlayerStrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };

        public static SKPaint TextMouseoverGroup { get; } = new()
        {
            Color = SKColors.LawnGreen,
            IsStroke = false,
            IsAntialias = true,
        };

        #endregion
    }
}
