/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using SkiaSharp;
using CameraManagerNew = LoneEftDmaRadar.Tarkov.GameWorld.Camera.CameraManager;

namespace LoneEftDmaRadar.UI.Skia.Rendering
{
    /// <summary>
    /// Handles rendering of loot items and corpses in the Aimview widget.
    /// </summary>
    public sealed class AimviewLootRenderer
    {
        private readonly SKBitmap _bitmap;
        private readonly SKCanvas _canvas;

        public AimviewLootRenderer(SKBitmap bitmap, SKCanvas canvas)
        {
            _bitmap = bitmap;
            _canvas = canvas;
        }

        public void DrawFilteredLoot(LocalPlayer localPlayer, IEnumerable<LootItem> lootItems)
        {
            if (!App.Config.AimviewWidget.ShowLoot || !App.Config.Loot.Enabled || lootItems is null)
                return;

            float maxDistance = App.Config.UI.AimviewLootRenderDistance <= 0
                ? float.MaxValue
                : App.Config.UI.AimviewLootRenderDistance;

            foreach (var item in lootItems)
            {
                if (item is StaticLootContainer || item is LootCorpse)
                    continue;

                if (item.IsQuestItem && !App.Config.AimviewWidget.ShowQuestItems)
                    continue;

                float distance = Vector3.Distance(localPlayer.Position, item.Position);
                if (distance > maxDistance)
                    continue;

                if (!TryProject(item.Position, out var screen, out float scale, localPlayer))
                    continue;

                float r = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);
                var (paint, textPaint) = GetLootPaints(item);

                _canvas.DrawCircle(screen.X, screen.Y, r, paint);

                var label = $"{item.GetUILabel()} D:{distance:F0}m";
                DrawLabel(label, screen, r, scale, textPaint);
            }
        }

        public void DrawCorpseMarkers(LocalPlayer localPlayer, IEnumerable<LootCorpse> corpses)
        {
            if (!App.Config.Loot.ShowCorpseMarkers || corpses is null)
                return;

            float maxRenderDistance = App.Config.UI.AimviewLootRenderDistance;
            bool unlimitedDistance = maxRenderDistance <= 0f;

            foreach (var corpse in corpses)
            {
                float distance = Vector3.Distance(localPlayer.Position, corpse.Position);
                if (!unlimitedDistance && distance > maxRenderDistance)
                    continue;

                if (!TryProject(corpse.Position, out var screen, out float scale, localPlayer))
                    continue;

                float r = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);
                var (shapePaint, textColor) = GetCorpsePaints(corpse);

                _canvas.DrawCircle(screen.X, screen.Y, r, shapePaint);

                float baseFontSize = SKFonts.EspWidgetFont.Size * scale * 0.9f;
                float fontSize = Math.Clamp(baseFontSize, 8f, 20f);
                using var font = new SKFont(SKFonts.EspWidgetFont.Typeface, fontSize) { Subpixel = true };

                float textY = screen.Y + r + 1;

                // Draw important items for AI corpses
                bool isAICorpse = corpse.Player?.IsAI ?? false;
                if (isAICorpse)
                {
                    textY = DrawImportantItems(corpse, screen, r, font, fontSize, textY);
                }

                // Draw corpse name
                using var textPaint = new SKPaint
                {
                    Color = textColor,
                    IsStroke = false,
                    IsAntialias = true
                };
                _canvas.DrawText($"{corpse.Name} D:{distance:F0}m", new SKPoint(screen.X + r + 3, textY), SKTextAlign.Left, font, textPaint);
            }
        }

        private float DrawImportantItems(LootCorpse corpse, SKPoint screen, float r, SKFont font, float fontSize, float textY)
        {
            var importantItems = corpse.GetAllImportantItems().ToList();
            foreach (var importantItem in importantItems)
            {
                SKColor importantColor;
                if (importantItem.Type == CorpseImportantItemType.Wishlist)
                {
                    importantColor = SKPaints.TextWishlistItem.Color;
                }
                else if (!string.IsNullOrEmpty(importantItem.CustomFilterColor) &&
                         SKColor.TryParse(importantItem.CustomFilterColor, out var filterColor))
                {
                    importantColor = filterColor;
                }
                else
                {
                    importantColor = SKPaints.TextImportantLoot.Color;
                }

                using var importantPaint = new SKPaint
                {
                    Color = importantColor,
                    IsStroke = false,
                    IsAntialias = true
                };
                _canvas.DrawText(importantItem.Label, new SKPoint(screen.X + r + 3, textY), SKTextAlign.Left, font, importantPaint);
                textY += fontSize + 2;
            }
            return textY;
        }

        private static (SKPaint paint, SKPaint textPaint) GetLootPaints(LootItem item)
        {
            if (item.IsQuestItem)
                return (SKPaints.PaintQuestItem, SKPaints.TextQuestItem);

            if (item.IsWishlisted)
                return (SKPaints.PaintWishlistItem, SKPaints.TextWishlistItem);

            if (item.IsBackpack)
                return (SKPaints.PaintBackpacks, SKPaints.TextBackpacks);

            if (item.IsMeds)
                return (SKPaints.PaintMeds, SKPaints.TextMeds);

            if (item.IsFood)
                return (SKPaints.PaintFood, SKPaints.TextFood);

            var filterColor = item.CustomFilter?.Color;
            if (!string.IsNullOrEmpty(filterColor) && SKColor.TryParse(filterColor, out var skColor))
            {
                var paint = new SKPaint
                {
                    Color = skColor,
                    StrokeWidth = 0.25f,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                var textPaint = new SKPaint
                {
                    Color = skColor,
                    IsStroke = false,
                    IsAntialias = true
                };
                return (paint, textPaint);
            }

            if (item.IsValuableLoot)
                return (SKPaints.PaintImportantLoot, SKPaints.TextImportantLoot);

            return (SKPaints.PaintFilteredLoot, SKPaints.TextFilteredLoot);
        }

        private static (SKPaint shape, SKColor textColor) GetCorpsePaints(LootCorpse corpse)
        {
            if (corpse.Player is null)
                return (SKPaints.PaintCorpse, SKPaints.PaintCorpse.Color);

            return corpse.Player.Type switch
            {
                PlayerType.PMC => (SKPaints.PaintPMC, SKPaints.TextPMC.Color),
                PlayerType.Teammate => (SKPaints.PaintTeammate, SKPaints.TextTeammate.Color),
                PlayerType.AIBoss => (SKPaints.PaintBoss, SKPaints.TextBoss.Color),
                PlayerType.AIRaider => (SKPaints.PaintRaider, SKPaints.TextRaider.Color),
                PlayerType.AIScav => (SKPaints.PaintScav, SKPaints.TextScav.Color),
                PlayerType.PScav => (SKPaints.PaintPScav, SKPaints.TextPScav.Color),
                PlayerType.SpecialPlayer => (SKPaints.PaintWatchlist, SKPaints.TextWatchlist.Color),
                PlayerType.Streamer => (SKPaints.PaintStreamer, SKPaints.TextStreamer.Color),
                _ => (SKPaints.PaintCorpse, SKPaints.PaintCorpse.Color)
            };
        }

        private void DrawLabel(string text, SKPoint screen, float radius, float scale, SKPaint textPaint)
        {
            float baseFontSize = SKFonts.EspWidgetFont.Size * scale * 0.9f;
            float fontSize = Math.Clamp(baseFontSize, 8f, 20f);
            using var font = new SKFont(SKFonts.EspWidgetFont.Typeface, fontSize) { Subpixel = true };
            _canvas.DrawText(text, new SKPoint(screen.X + radius + 3, screen.Y + radius + 1), SKTextAlign.Left, font, textPaint);
        }

        private bool TryProject(in Vector3 world, out SKPoint scr, out float scale, LocalPlayer localPlayer)
        {
            scr = default;
            scale = 1f;

            if (world == Vector3.Zero)
                return false;

            if (!CameraManagerNew.WorldToScreen(in world, out var espScreen, false, false))
                return false;

            var viewport = CameraManagerNew.Viewport;
            if (viewport.Width <= 0 || viewport.Height <= 0)
                return false;

            float relX = espScreen.X / viewport.Width;
            float relY = espScreen.Y / viewport.Height;

            scr = new SKPoint(relX * _bitmap.Width, relY * _bitmap.Height);

            Vector3 refPos = localPlayer?.Position ?? CameraManagerNew.CameraPosition;
            float dist = Vector3.Distance(refPos, world);

            scale = Math.Clamp(
                SKConstants.AimviewReferenceDistance / Math.Max(dist, 1f),
                SKConstants.AimviewPerspectiveScaleMin,
                SKConstants.AimviewPerspectiveScaleMax);

            if (scr.X < -SKConstants.AimviewEdgeTolerance || scr.X > _bitmap.Width + SKConstants.AimviewEdgeTolerance ||
                scr.Y < -SKConstants.AimviewEdgeTolerance || scr.Y > _bitmap.Height + SKConstants.AimviewEdgeTolerance)
            {
                return false;
            }

            return true;
        }
    }
}
