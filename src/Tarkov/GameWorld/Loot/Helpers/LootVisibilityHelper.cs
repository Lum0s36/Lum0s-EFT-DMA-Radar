/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.Tarkov.GameWorld.Quests;
using LoneEftDmaRadar.UI.Skia;
using SkiaSharp;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Loot.Helpers
{
    /// <summary>
    /// Shared helper for loot visibility and color logic.
    /// Used by both Radar/Aimview and ESP to ensure consistent behavior.
    /// </summary>
    public static class LootVisibilityHelper
    {
        #region Visibility Logic

        /// <summary>
        /// Determines whether a loot item should be displayed based on current settings.
        /// Uses the default MemoryGameStateProvider for quest manager access.
        /// </summary>
        /// <param name="item">The loot item to check.</param>
        /// <param name="context">The rendering context (ESP or Radar).</param>
        /// <returns>True if the item should be shown, false otherwise.</returns>
        public static bool ShouldShowItem(LootItem item, LootRenderContext context)
        {
            return ShouldShowItem(item, context, MemoryGameStateProvider.Instance);
        }

        /// <summary>
        /// Determines whether a loot item should be displayed based on current settings.
        /// </summary>
        /// <param name="item">The loot item to check.</param>
        /// <param name="context">The rendering context (ESP or Radar).</param>
        /// <param name="gameState">Game state provider for accessing quest manager.</param>
        /// <returns>True if the item should be shown, false otherwise.</returns>
        public static bool ShouldShowItem(LootItem item, LootRenderContext context, IGameStateProvider gameState)
        {
            // Skip containers and airdrops (handled separately)
            if (item is StaticLootContainer or LootAirdrop)
                return false;

            // Get item properties
            bool isCorpse = item is LootCorpse;
            bool isQuest = item.IsQuestItem;
            bool isFood = item.IsFood;
            bool isMeds = item.IsMeds;
            bool isBackpack = item.IsBackpack;
            bool isWishlisted = item.IsWishlisted;
            bool isInFilter = item.CustomFilter != null || (item.IsImportant && !item.IsWishlisted);

            // Quest items (highest priority)
            if (isQuest && context.ShowQuestItems)
            {
                var questManager = gameState?.QuestManager;
                if (questManager == null || questManager.IsQuestItem(item.ID))
                    return true;
            }

            // Corpses
            if (isCorpse && context.ShowCorpses)
                return true;

            // Wishlist items
            if (isWishlisted && context.ShowWishlisted)
                return true;

            // Filter-only mode: only show items in filter
            if (context.FilterOnlyMode)
            {
                return isInFilter;
            }

            // Filter items (when not in filter-only mode)
            if (isInFilter && context.ShowLoot)
                return true;

            // Meds
            if (isMeds && context.ShowMeds)
                return true;

            // Food
            if (isFood && context.ShowFood)
                return true;

            // Backpacks
            if (isBackpack && context.ShowBackpacks)
                return true;

            // Regular/valuable loot
            if (context.ShowLoot && (item.IsRegularLoot || item.IsValuableLoot))
                return true;

            return false;
        }

        /// <summary>
        /// Gets the loot type for color determination.
        /// </summary>
        public static LootColorType GetLootColorType(LootItem item)
        {
            if (item.IsQuestItem)
                return LootColorType.Quest;

            if (item.IsWishlisted)
                return LootColorType.Wishlist;

            if (item is LootCorpse)
                return LootColorType.Corpse;

            if (item.CustomFilter != null)
                return LootColorType.CustomFilter;

            if (item.IsImportant || item.IsValuableLoot || item is LootAirdrop)
                return LootColorType.Important;

            if (item.IsBackpack)
                return LootColorType.Backpack;

            if (item.IsMeds)
                return LootColorType.Meds;

            if (item.IsFood)
                return LootColorType.Food;

            return LootColorType.Regular;
        }

        /// <summary>
        /// Gets the custom filter color if available.
        /// </summary>
        public static string GetCustomFilterColor(LootItem item)
        {
            return item.CustomFilter?.Color;
        }

        #endregion

        #region SKPaint Colors (for Radar/Aimview)

        /// <summary>
        /// Gets the SKPaint pair (shape, text) for a loot item.
        /// </summary>
        public static (SKPaint shape, SKPaint text) GetSkPaints(LootItem item)
        {
            var colorType = GetLootColorType(item);

            return colorType switch
            {
                LootColorType.Quest => (SKPaints.PaintQuestItem, SKPaints.TextQuestItem),
                LootColorType.Wishlist => (SKPaints.PaintWishlistItem, SKPaints.TextWishlistItem),
                LootColorType.Corpse => (SKPaints.PaintCorpse, SKPaints.TextCorpse),
                LootColorType.CustomFilter => GetCustomFilterPaints(item),
                LootColorType.Important => (SKPaints.PaintImportantLoot, SKPaints.TextImportantLoot),
                LootColorType.Backpack => (SKPaints.PaintBackpacks, SKPaints.TextBackpacks),
                LootColorType.Meds => (SKPaints.PaintMeds, SKPaints.TextMeds),
                LootColorType.Food => (SKPaints.PaintFood, SKPaints.TextFood),
                _ => (SKPaints.PaintLoot, SKPaints.TextLoot)
            };
        }

        private static (SKPaint shape, SKPaint text) GetCustomFilterPaints(LootItem item)
        {
            var color = item.CustomFilter?.Color;
            if (string.IsNullOrEmpty(color) || !SKColor.TryParse(color, out var skColor))
                return (SKPaints.PaintLoot, SKPaints.TextLoot);

            // Use existing cached paints from LootItem
            var cached = GetCachedFilterPaint(color, skColor);
            return cached;
        }

        // Cache for custom filter paints
        private static readonly ConcurrentDictionary<string, (SKPaint shape, SKPaint text)> _filterPaintCache = new();

        private static (SKPaint shape, SKPaint text) GetCachedFilterPaint(string colorKey, SKColor skColor)
        {
            return _filterPaintCache.GetOrAdd(colorKey, _ =>
            {
                var shape = new SKPaint
                {
                    Color = skColor,
                    StrokeWidth = LootConstants.FilterPaintStrokeWidth * App.Config.UI.UIScale,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                var text = new SKPaint
                {
                    Color = skColor,
                    IsStroke = false,
                    IsAntialias = true
                };
                return (shape, text);
            });
        }

        /// <summary>
        /// Updates stroke width on cached filter paints when UI scale changes.
        /// </summary>
        public static void ScaleFilterPaints(float newScale)
        {
            foreach (var paints in _filterPaintCache.Values)
            {
                paints.shape.StrokeWidth = LootConstants.FilterPaintStrokeWidth * newScale;
            }
        }

        #endregion
    }

    /// <summary>
    /// Rendering context for loot visibility checks.
    /// Contains all the config flags needed for visibility determination.
    /// </summary>
    public readonly struct LootRenderContext
    {
        /// <summary>
        /// Creates a context from current ESP config settings.
        /// </summary>
        public static LootRenderContext FromEspConfig()
        {
            var ui = App.Config.UI;
            return new LootRenderContext
            {
                ShowLoot = ui.EspLoot,
                ShowQuestItems = ui.EspQuestLoot,
                ShowCorpses = ui.EspCorpses,
                ShowWishlisted = ui.EspShowWishlisted,
                ShowMeds = ui.EspMeds,
                ShowFood = ui.EspFood,
                ShowBackpacks = ui.EspBackpacks,
                FilterOnlyMode = ui.EspLootFilterOnly
            };
        }

        /// <summary>
        /// Creates a context from current Radar config settings.
        /// </summary>
        public static LootRenderContext FromRadarConfig()
        {
            var loot = App.Config.Loot;
            return new LootRenderContext
            {
                ShowLoot = loot.Enabled,
                ShowQuestItems = loot.ShowQuestItems,
                ShowCorpses = !loot.HideCorpses,
                ShowWishlisted = loot.ShowWishlistedRadar,
                ShowMeds = UI.Loot.LootFilter.ShowMeds,
                ShowFood = UI.Loot.LootFilter.ShowFood,
                ShowBackpacks = UI.Loot.LootFilter.ShowBackpacks,
                FilterOnlyMode = false // Radar uses filter predicate differently
            };
        }

        public bool ShowLoot { get; init; }
        public bool ShowQuestItems { get; init; }
        public bool ShowCorpses { get; init; }
        public bool ShowWishlisted { get; init; }
        public bool ShowMeds { get; init; }
        public bool ShowFood { get; init; }
        public bool ShowBackpacks { get; init; }
        public bool FilterOnlyMode { get; init; }
    }

    /// <summary>
    /// Loot color types for consistent coloring across Radar and ESP.
    /// </summary>
    public enum LootColorType
    {
        Regular,
        Important,
        Quest,
        Wishlist,
        Corpse,
        Meds,
        Food,
        Backpack,
        CustomFilter
    }
}
