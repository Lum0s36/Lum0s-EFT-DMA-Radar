/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

namespace LoneEftDmaRadar.UI.Skia
{
    /// <summary>
    /// Paint definitions for loot rendering.
    /// </summary>
    internal static class SKLootPaints
    {
        #region Basic Loot

        public static SKPaint PaintLoot { get; } = new()
        {
            Color = SKColors.WhiteSmoke,
            StrokeWidth = SKConstants.LootStrokeWidth,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        public static SKPaint TextLoot { get; } = new()
        {
            Color = SKColors.WhiteSmoke,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint PaintImportantLoot { get; } = new()
        {
            Color = SKColors.Turquoise,
            StrokeWidth = SKConstants.LootStrokeWidth,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        public static SKPaint TextImportantLoot { get; } = new()
        {
            Color = SKColors.Turquoise,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint PaintFilteredLoot { get; } = new()
        {
            Color = SKColors.MediumPurple,
            StrokeWidth = SKConstants.LootStrokeWidth,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        public static SKPaint TextFilteredLoot { get; } = new()
        {
            Color = SKColors.MediumPurple,
            IsStroke = false,
            IsAntialias = true,
        };

        #endregion

        #region Containers & Corpses

        public static SKPaint PaintContainerLoot { get; } = new()
        {
            Color = SKColor.Parse(SKConstants.ContainerColorHex),
            StrokeWidth = SKConstants.LootStrokeWidth,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        public static SKPaint PaintCorpse { get; } = new()
        {
            Color = SKColors.Silver,
            StrokeWidth = SKConstants.LootStrokeWidth,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        public static SKPaint TextCorpse { get; } = new()
        {
            Color = SKColors.Silver,
            IsStroke = false,
            IsAntialias = true,
        };

        #endregion

        #region Category Loot

        public static SKPaint PaintMeds { get; } = new()
        {
            Color = SKColors.LightSalmon,
            StrokeWidth = SKConstants.LootStrokeWidth,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        public static SKPaint TextMeds { get; } = new()
        {
            Color = SKColors.LightSalmon,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint PaintFood { get; } = new()
        {
            Color = SKColors.CornflowerBlue,
            StrokeWidth = SKConstants.LootStrokeWidth,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        public static SKPaint TextFood { get; } = new()
        {
            Color = SKColors.CornflowerBlue,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint PaintBackpacks { get; } = new()
        {
            Color = SKColor.Parse(SKConstants.BackpackColorHex),
            StrokeWidth = SKConstants.LootStrokeWidth,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        public static SKPaint TextBackpacks { get; } = new()
        {
            Color = SKColor.Parse(SKConstants.BackpackColorHex),
            IsStroke = false,
            IsAntialias = true,
        };

        #endregion

        #region Quest & Wishlist

        public static SKPaint PaintQuestItem { get; } = new()
        {
            Color = SKColors.YellowGreen,
            StrokeWidth = SKConstants.LootStrokeWidth,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        public static SKPaint TextQuestItem { get; } = new()
        {
            Color = SKColors.YellowGreen,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint PaintWishlistItem { get; } = new()
        {
            Color = SKColors.Red,
            StrokeWidth = SKConstants.LootStrokeWidth,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        public static SKPaint TextWishlistItem { get; } = new()
        {
            Color = SKColors.Red,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint QuestHelperPaint { get; } = new()
        {
            Color = SKColors.DeepPink,
            StrokeWidth = SKConstants.LootStrokeWidth,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        public static SKPaint QuestHelperText { get; } = new()
        {
            Color = SKColors.DeepPink,
            IsStroke = false,
            IsAntialias = true,
        };

        public static SKPaint PaintQuestZone { get; } = new()
        {
            Color = SKColors.Yellow,
            StrokeWidth = SKConstants.QuestZoneStrokeWidth,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        public static SKPaint TextQuestZone { get; } = new()
        {
            Color = SKColors.Yellow,
            IsStroke = false,
#pragma warning disable CS0618
            TextSize = SKConstants.QuestZoneTextSize,
            TextEncoding = SKTextEncoding.Utf8,
#pragma warning restore CS0618
            IsAntialias = true,
        };

        #endregion
    }
}
