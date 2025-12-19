/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Misc;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot.Helpers;
using LoneEftDmaRadar.UI.Skia;
using SharpDX.Mathematics.Interop;
using SkiaSharp;
using DxColor = SharpDX.Mathematics.Interop.RawColorBGRA;

namespace LoneEftDmaRadar.UI.ESP.Rendering
{
    /// <summary>
    /// Handles rendering of loot items on the ESP overlay.
    /// Uses shared LootVisibilityHelper for consistent visibility logic with Radar.
    /// </summary>
    internal sealed class EspLootRenderer
    {
        #region Public Methods

        /// <summary>
        /// Renders all visible loot items on the ESP overlay.
        /// </summary>
        public void Draw(EspRenderContext context)
        {
            // Use context.Loot instead of Memory.Game?.Loot
            var lootItems = context.Loot?.AllLoot;
            if (lootItems == null)
                return;

            // Create visibility context from ESP config
            var visibilityContext = LootRenderContext.FromEspConfig();

            foreach (var item in lootItems)
            {
                // Skip containers and airdrops (handled separately)
                if (item is StaticLootContainer or LootAirdrop)
                    continue;

                DrawLootItem(context, item, visibilityContext);
            }
        }

        /// <summary>
        /// Renders static containers on the ESP overlay.
        /// </summary>
        public void DrawContainers(EspRenderContext context)
        {
            if (!App.Config.UI.EspContainers)
                return;

            // Use context.Loot instead of Memory.Game?.Loot
            var containers = context.Loot?.AllLoot?.OfType<StaticLootContainer>();
            if (containers == null)
                return;

            bool selectAll = App.Config.Containers.SelectAll;
            var selected = App.Config.Containers.Selected;
            bool hideSearched = App.Config.Containers.HideSearched;
            float maxRenderDistance = App.Config.Containers.EspDrawDistance;

            foreach (var container in containers)
            {
                var id = container.ID ?? "UNKNOWN";
                if (!selectAll && !selected.ContainsKey(id))
                    continue;

                if (hideSearched && container.Searched)
                    continue;

                if (!context.IsWithinDistance(container.Position, maxRenderDistance))
                    continue;

                if (!context.WorldToScreenWithScale(container.Position, out var screen, out float scale))
                    continue;

                float radius = context.GetScaledRadius(scale);
                var color = EspColorHelper.GetContainerColor();

                context.Ctx.DrawCircle(ToRaw(screen), radius, color, true);

                var textSize = context.GetTextSize(scale);
                context.Ctx.DrawText(
                    container.Name ?? "Container",
                    screen.X + radius + ESPConstants.TextOffsetFromMarker,
                    screen.Y + ESPConstants.TextOffsetFromMarker,
                    color,
                    textSize);
            }
        }

        #endregion

        #region Private Methods

        private void DrawLootItem(EspRenderContext context, LootItem item, LootRenderContext visibilityContext)
        {
            // Use shared visibility helper
            if (!LootVisibilityHelper.ShouldShowItem(item, visibilityContext))
                return;

            // Check distance limit
            float maxRenderDistance = App.Config.UI.EspLootMaxDistance;
            if (!context.IsWithinDistance(item.Position, maxRenderDistance))
                return;

            // Project to screen
            if (!context.WorldToScreenWithScale(item.Position, out var screen, out float scale))
                return;

            // Check cone filter (wishlisted items always shown)
            bool isWishlisted = item.IsWishlisted;
            if (!IsInConeFilter(screen, context.ScreenWidth, context.ScreenHeight))
            {
                if (!isWishlisted)
                    return;
            }

            float distance = context.DistanceTo(item.Position);

            // Get color using shared helper
            var color = GetLootItemColor(item);

            // Draw marker
            float radius = context.GetScaledRadius(scale);
            context.Ctx.DrawCircle(ToRaw(screen), radius, color, true);

            // Draw label (if in cone or wishlisted)
            if (isWishlisted || IsInConeFilter(screen, context.ScreenWidth, context.ScreenHeight))
            {
                string text = GetLootLabel(item);
                if (App.Config.UI.EspLootPrice)
                {
                    text = $"{text} D:{distance:F0}m";
                }
                else
                {
                    text = $"{text} ({distance:F0}m)";
                }

                var textSize = context.GetTextSize(scale);
                context.Ctx.DrawText(
                    text,
                    screen.X + radius + ESPConstants.TextOffsetFromMarker,
                    screen.Y + ESPConstants.TextOffsetFromMarker,
                    color,
                    textSize);
            }
        }

        private bool IsInConeFilter(SKPoint screen, float screenWidth, float screenHeight)
        {
            if (!App.Config.UI.EspLootConeEnabled || App.Config.UI.EspLootConeAngle <= 0f)
                return true;

            float centerX = screenWidth / 2f;
            float centerY = screenHeight / 2f;
            float dx = screen.X - centerX;
            float dy = screen.Y - centerY;
            float fov = App.Config.UI.FOV;
            float screenAngleX = MathF.Abs(dx / centerX) * (fov / 2f);
            float screenAngleY = MathF.Abs(dy / centerY) * (fov / 2f);
            float screenAngle = MathF.Sqrt(screenAngleX * screenAngleX + screenAngleY * screenAngleY);

            return screenAngle <= App.Config.UI.EspLootConeAngle;
        }

        #endregion

        #region Color Logic

        /// <summary>
        /// Gets the ESP color for a loot item using shared LootVisibilityHelper.
        /// </summary>
        private DxColor GetLootItemColor(LootItem item)
        {
            var colorType = LootVisibilityHelper.GetLootColorType(item);

            return colorType switch
            {
                LootColorType.Quest => EspColorHelper.GetQuestItemColor(),
                LootColorType.Wishlist => EspColorHelper.GetWishlistItemColor(),
                LootColorType.Corpse => EspColorHelper.GetCorpseColor(),
                LootColorType.CustomFilter => GetCustomFilterEspColor(item),
                LootColorType.Important => EspColorHelper.GetImportantLootColor(),
                LootColorType.Backpack => EspColorHelper.GetBackpackColor(),
                LootColorType.Meds => EspColorHelper.GetMedsColor(),
                LootColorType.Food => EspColorHelper.GetFoodColor(),
                _ => EspColorHelper.GetLootColor()
            };
        }

        private DxColor GetCustomFilterEspColor(LootItem item)
        {
            var filterColor = LootVisibilityHelper.GetCustomFilterColor(item);
            if (!string.IsNullOrEmpty(filterColor) && SKColor.TryParse(filterColor, out var skColor))
            {
                return EspColorHelper.ToColor(skColor);
            }
            return EspColorHelper.GetImportantLootColor();
        }

        #endregion

        #region Label Generation

        private string GetLootLabel(LootItem item)
        {
            // Corpse name
            if (item is LootCorpse corpse && !string.IsNullOrWhiteSpace(corpse.Player?.Name))
                return corpse.Player.Name;

            var label = "";

            // Use the shared UI label from LootItem
            if (item.IsWishlisted)
            {
                label = "!! " + item.ShortName;
            }
            else if (App.Config.UI.EspLootPrice && item.Price > 0)
            {
                label = $"[{Utilities.FormatNumberKM(item.Price)}] {item.ShortName}";
            }
            else
            {
                label = item.ShortName;
            }

            return string.IsNullOrEmpty(label) ? "Item" : label;
        }

        #endregion

        #region Utility

        private static RawVector2 ToRaw(SKPoint point) => new(point.X, point.Y);

        #endregion
    }
}
