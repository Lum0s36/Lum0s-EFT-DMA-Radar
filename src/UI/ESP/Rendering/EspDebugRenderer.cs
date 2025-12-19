/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using DxColor = SharpDX.Mathematics.Interop.RawColorBGRA;

namespace LoneEftDmaRadar.UI.ESP.Rendering
{
    /// <summary>
    /// Handles rendering of debug overlays on the ESP.
    /// </summary>
    internal sealed class EspDebugRenderer
    {
        #region Public Methods

        /// <summary>
        /// Draws the loot debug overlay showing filter/visibility statistics.
        /// </summary>
        public void DrawLootDebugOverlay(EspRenderContext context)
        {
            if (!App.Config.UI.EspLootDebug)
                return;

            // Use context.Loot instead of Memory.Game?.Loot
            var lootItems = context.Loot?.AllLoot;
            if (lootItems == null)
            {
                context.Ctx.DrawText("Loot Debug: No loot items", 10, 30, EspColorHelper.White, DxTextSize.Small);
                return;
            }

            var stats = CalculateLootStats(context, lootItems);
            DrawDebugStats(context, stats);
        }

        #endregion

        #region Private Methods

        private LootDebugStats CalculateLootStats(EspRenderContext context, IEnumerable<LootItem> lootItems)
        {
            var stats = new LootDebugStats();
            var camPos = context.LocalPlayer?.Position ?? Vector3.Zero;
            float maxRenderDistance = App.Config.UI.EspLootMaxDistance;
            bool unlimitedDistance = maxRenderDistance <= 0f;

            foreach (var item in lootItems)
            {
                if (item is StaticLootContainer or LootAirdrop)
                    continue;

                stats.TotalItems++;

                bool isCorpse = item is LootCorpse;
                bool isQuest = item.IsQuestItem;
                bool isFood = item.IsFood;
                bool isMeds = item.IsMeds;
                bool isBackpack = item.IsBackpack;
                bool isInFilter = item.CustomFilter != null || (item.IsImportant && !item.IsWishlisted);
                bool isWishlisted = item.IsWishlisted;

                // Count by category
                if (item.CustomFilter != null) stats.ItemsWithCustomFilter++;
                if (item.IsImportant) stats.ItemsWithImportant++;
                if (item.IsImportant && !item.IsWishlisted) stats.ItemsWithImportantNotWishlist++;
                if (isInFilter) stats.TotalInFilter++;
                if (isWishlisted) stats.TotalWishlisted++;
                if (isMeds) stats.TotalMeds++;
                if (isFood) stats.TotalFood++;
                if (!isMeds && !isFood && !isQuest && !isCorpse) stats.TotalRegular++;

                // Check distance
                float distance = Vector3.Distance(camPos, item.Position);
                if (!unlimitedDistance && distance > maxRenderDistance)
                    continue;

                // Apply filters
                if (isQuest && !App.Config.UI.EspQuestLoot)
                    continue;
                if (isCorpse && !App.Config.UI.EspCorpses)
                    continue;

                bool shouldShow = false;

                if (App.Config.UI.EspLootFilterOnly)
                {
                    if (isInFilter)
                    {
                        shouldShow = true;
                        stats.ShownInFilter++;
                    }
                    else if (App.Config.UI.EspShowWishlisted && isWishlisted)
                    {
                        shouldShow = true;
                        stats.ShownWishlist++;
                    }
                }
                else if (App.Config.UI.EspShowWishlisted && isWishlisted)
                {
                    shouldShow = true;
                    stats.ShownWishlist++;
                }
                else if (isMeds)
                {
                    if (App.Config.UI.EspShowWishlisted && isWishlisted)
                    {
                        shouldShow = true;
                        stats.ShownMeds++;
                        stats.ShownWishlist++;
                    }
                    else if (App.Config.UI.EspMeds)
                    {
                        shouldShow = true;
                        stats.ShownMeds++;
                    }
                }
                else if (isFood)
                {
                    if (App.Config.UI.EspShowWishlisted && isWishlisted)
                    {
                        shouldShow = true;
                        stats.ShownFood++;
                        stats.ShownWishlist++;
                    }
                    else if (App.Config.UI.EspFood)
                    {
                        shouldShow = true;
                        stats.ShownFood++;
                    }
                }
                else if (App.Config.UI.EspLoot)
                {
                    if (!isInFilter && !isWishlisted && (item.IsRegularLoot || item.IsValuableLoot))
                    {
                        shouldShow = true;
                        stats.ShownRegular++;
                    }
                }

                if (shouldShow)
                {
                    stats.ShownItems++;
                    if (isInFilter) stats.FilteredItems++;
                }
            }

            return stats;
        }

        private void DrawDebugStats(EspRenderContext context, LootDebugStats stats)
        {
            float y = 30f;
            float lineHeight = 16f;
            var textColor = EspColorHelper.White;
            var highlightColor = new DxColor(0, 255, 0, 255);
            var errorColor = new DxColor(255, 0, 0, 255);
            var warningColor = new DxColor(255, 255, 0, 255);

            DrawLine(context, "=== ESP Loot Debug ===", 10, ref y, lineHeight, textColor);

            DrawLine(context, $"EspLootFilterOnly: {App.Config.UI.EspLootFilterOnly}", 10, ref y, lineHeight,
                App.Config.UI.EspLootFilterOnly ? highlightColor : textColor);

            DrawLine(context, $"EspShowWishlisted: {App.Config.UI.EspShowWishlisted}", 10, ref y, lineHeight,
                App.Config.UI.EspShowWishlisted ? highlightColor : textColor);

            DrawLine(context, $"EspLoot: {App.Config.UI.EspLoot}", 10, ref y, lineHeight,
                App.Config.UI.EspLoot ? highlightColor : textColor);

            DrawLine(context, $"EspMeds: {App.Config.UI.EspMeds}", 10, ref y, lineHeight,
                App.Config.UI.EspMeds ? highlightColor : textColor);

            DrawLine(context, $"EspFood: {App.Config.UI.EspFood}", 10, ref y, lineHeight,
                App.Config.UI.EspFood ? highlightColor : textColor);

            y += lineHeight;
            DrawLine(context, $"Total Items: {stats.TotalItems}", 10, ref y, lineHeight, textColor);

            DrawLine(context, $"Shown Items: {stats.ShownItems}", 10, ref y, lineHeight,
                stats.ShownItems > 0 ? highlightColor : errorColor);

            DrawLine(context, $"  - In Filter: {stats.ShownInFilter} | Wishlist: {stats.ShownWishlist}", 10, ref y, lineHeight, textColor);
            DrawLine(context, $"  - Meds: {stats.ShownMeds} | Food: {stats.ShownFood} | Regular: {stats.ShownRegular}", 10, ref y, lineHeight, textColor);

            DrawLine(context, $"Total In Filter: {stats.TotalInFilter} | Wishlisted: {stats.TotalWishlisted}", 10, ref y, lineHeight,
                stats.TotalInFilter > 0 ? highlightColor : errorColor);

            DrawLine(context, $"  - CustomFilter: {stats.ItemsWithCustomFilter} | Important: {stats.ItemsWithImportant} | Important(!Wishlist): {stats.ItemsWithImportantNotWishlist}", 10, ref y, lineHeight, textColor);
            DrawLine(context, $"Shown Wishlist: {stats.ShownWishlist} (out of {stats.TotalWishlisted} total)", 10, ref y, lineHeight,
                stats.ShownWishlist > 0 ? highlightColor : textColor);

            DrawLine(context, $"Total - Meds: {stats.TotalMeds} | Food: {stats.TotalFood} | Regular: {stats.TotalRegular}", 10, ref y, lineHeight, textColor);

            y += lineHeight;
            string modeText;
            DxColor modeColor;

            if (App.Config.UI.EspLootFilterOnly && !App.Config.UI.EspLoot)
            {
                modeText = "Mode: FILTER_ONLY (should only show filter items)";
                modeColor = highlightColor;
            }
            else if (!App.Config.UI.EspLootFilterOnly && App.Config.UI.EspLoot)
            {
                modeText = "Mode: LOOT_ONLY (showing regular loot)";
                modeColor = warningColor;
            }
            else if (!App.Config.UI.EspLootFilterOnly && !App.Config.UI.EspLoot)
            {
                modeText = "Mode: ALL_OFF (should hide all)";
                modeColor = stats.ShownItems > 0 ? errorColor : highlightColor;
            }
            else
            {
                modeText = "Mode: MIXED";
                modeColor = warningColor;
            }

            DrawLine(context, modeText, 10, ref y, lineHeight, modeColor);
        }

        private void DrawLine(EspRenderContext context, string text, float x, ref float y, float lineHeight, DxColor color)
        {
            context.Ctx.DrawText(text, x, y, color, DxTextSize.Small);
            y += lineHeight;
        }

        #endregion

        #region Helper Struct

        private struct LootDebugStats
        {
            public int TotalItems;
            public int ShownItems;
            public int FilteredItems;
            public int TotalMeds;
            public int TotalFood;
            public int TotalRegular;
            public int TotalInFilter;
            public int TotalWishlisted;
            public int ShownMeds;
            public int ShownFood;
            public int ShownRegular;
            public int ShownInFilter;
            public int ShownWishlist;
            public int ItemsWithCustomFilter;
            public int ItemsWithImportant;
            public int ItemsWithImportantNotWishlist;
        }

        #endregion
    }
}
