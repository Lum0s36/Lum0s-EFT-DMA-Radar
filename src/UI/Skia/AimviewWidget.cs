/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * 
 * Exfil Status Tracking: Credit to Keegi
 * 
MIT License

Copyright (c) 2025 Lone DMA

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESSED OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 *
*/

using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using LoneEftDmaRadar.Tarkov.GameWorld.Exits;
using LoneEftDmaRadar.Tarkov.GameWorld.Explosives;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot; // ✅ Add for StaticLootContainer
using LoneEftDmaRadar.Tarkov.Unity.Structures; // for Bones enum
using LoneEftDmaRadar.UI.Misc;
using SkiaSharp.Views.WPF;
using CameraManagerNew = LoneEftDmaRadar.Tarkov.GameWorld.Camera.CameraManager;

namespace LoneEftDmaRadar.UI.Skia
{
    public sealed class AimviewWidget : AbstractSKWidget
    {
        // Fields
        private SKBitmap _bitmap;
        private SKCanvas _canvas;

        public AimviewWidget(SKGLElement parent, SKRect location, bool minimized, float scale)
            : base(parent, "Aimview",
                new SKPoint(location.Left, location.Top),
                new SKSize(location.Width, location.Height),
                scale)
        {
            AllocateSurface((int)location.Width, (int)location.Height);
            Minimized = minimized;
        }

        private static LocalPlayer LocalPlayer => Memory.LocalPlayer;
        private static IReadOnlyCollection<AbstractPlayer> AllPlayers => Memory.Players;
        private static IReadOnlyCollection<IExitPoint> Exits => Memory.Exits;
        private static IReadOnlyCollection<IExplosiveItem> Explosives => Memory.Explosives;
        private static bool InRaid => Memory.InRaid;

        public override void Draw(SKCanvas canvas)
        {
            base.Draw(canvas);
            if (Minimized)
                return;

            RenderESPWidget(canvas, ClientRectangle);
        }

        private void RenderESPWidget(SKCanvas targetCanvas, SKRect dest)
        {
            EnsureSurface(Size);

            _canvas.Clear(SKColors.Transparent);

            try
            {
                if (!InRaid)
                    return;

                if (LocalPlayer is not LocalPlayer localPlayer)
                    return;

                DrawExfils(localPlayer);
                DrawExplosives(localPlayer);
                DrawStaticContainers(localPlayer);
                DrawCorpseMarkers(localPlayer); // ✅ Add corpse marker drawing
                DrawPlayersAndAIAsSkeletons(localPlayer);
                DrawFilteredLoot(localPlayer);
                DrawCrosshair();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"CRITICAL AIMVIEW WIDGET RENDER ERROR: {ex}");
            }

            _canvas.Flush();
            targetCanvas.DrawBitmap(_bitmap, dest, SKPaints.PaintBitmap);
        }

        // Skeleton connections reused for Aimview
        private static readonly (Bones From, Bones To)[] _boneConnections = new[]
        {
            (Bones.HumanHead, Bones.HumanNeck),
            (Bones.HumanNeck, Bones.HumanSpine3),
            (Bones.HumanSpine3, Bones.HumanSpine2),
            (Bones.HumanSpine2, Bones.HumanSpine1),
            (Bones.HumanSpine1, Bones.HumanPelvis),
            // Left Arm
            (Bones.HumanNeck, Bones.HumanLUpperarm),
            (Bones.HumanLUpperarm, Bones.HumanLForearm1),
            (Bones.HumanLForearm1, Bones.HumanLForearm2),
            (Bones.HumanLForearm2, Bones.HumanLPalm),
            // Right Arm
            (Bones.HumanNeck, Bones.HumanRUpperarm),
            (Bones.HumanRUpperarm, Bones.HumanRForearm1),
            (Bones.HumanRForearm1, Bones.HumanRForearm2),
            (Bones.HumanRForearm2, Bones.HumanRPalm),
            // Left Leg
            (Bones.HumanPelvis, Bones.HumanLThigh1),
            (Bones.HumanLThigh1, Bones.HumanLThigh2),
            (Bones.HumanLThigh2, Bones.HumanLCalf),
            (Bones.HumanLCalf, Bones.HumanLFoot),
            // Right Leg
            (Bones.HumanPelvis, Bones.HumanRThigh1),
            (Bones.HumanRThigh1, Bones.HumanRThigh2),
            (Bones.HumanRThigh2, Bones.HumanRCalf),
            (Bones.HumanRCalf, Bones.HumanRFoot),
        };

        private void DrawExfils(LocalPlayer localPlayer)
        {
            // Controlled by Aimview-specific "Show Exfils" checkbox
            if (!App.Config.AimviewWidget.ShowExfils)
                return;

            if (Exits is null)
                return;

            // Exfils are ALWAYS rendered regardless of distance settings
            // They are important navigation points and should never be hidden by distance sliders

            foreach (var exit in Exits)
            {
                // Handle regular Exfils with status tracking (Credit: Keegi)
                if (exit is Exfil exfil)
                {
                    // Skip closed exfils
                    if (exfil.Status == Exfil.EStatus.Closed)
                        continue;

                    if (TryProject(exfil.Position, out var screen, out float scale, localPlayer))
                    {
                        // Status-based paint selection (Credit: Keegi)
                        var paint = exfil.Status switch
                        {
                            Exfil.EStatus.Open => SKPaints.PaintExfilOpen,
                            Exfil.EStatus.Pending => SKPaints.PaintExfilPending,
                            _ => SKPaints.PaintExfilOpen
                        };
                        
                        float distance = Vector3.Distance(localPlayer.Position, exfil.Position);

                        // Scale radius with perspective (from TryProject)
                        float r = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);

                        _canvas.DrawCircle(screen.X, screen.Y, r, paint);

                        // Build label with status
                        var statusText = exfil.Status switch
                        {
                            Exfil.EStatus.Open => "",
                            Exfil.EStatus.Pending => " [Pending]",
                            _ => ""
                        };

                        // Scale font with perspective
                        float baseFontSize = SKFonts.EspWidgetFont.Size * scale * 0.9f;
                        float fontSize = Math.Clamp(baseFontSize, 8f, 20f);
                        using var font = new SKFont(SKFonts.EspWidgetFont.Typeface, fontSize) { Subpixel = true };
                        _canvas.DrawText($"{exfil.Name}{statusText} D:{distance:F0}m", new SKPoint(screen.X + r + 3, screen.Y + r + 1), SKTextAlign.Left, font, SKPaints.TextExfil);
                    }
                }
                // Handle TransitPoints (no status tracking - always shown as orange)
                else if (exit is TransitPoint transit)
                {
                    if (TryProject(transit.Position, out var screen, out float scale, localPlayer))
                    {
                        var paint = SKPaints.PaintExfilTransit;
                        float distance = Vector3.Distance(localPlayer.Position, transit.Position);

                        // Scale radius with perspective (from TryProject)
                        float r = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);

                        _canvas.DrawCircle(screen.X, screen.Y, r, paint);

                        // Scale font with perspective
                        float baseFontSize = SKFonts.EspWidgetFont.Size * scale * 0.9f;
                        float fontSize = Math.Clamp(baseFontSize, 8f, 20f);
                        using var font = new SKFont(SKFonts.EspWidgetFont.Typeface, fontSize) { Subpixel = true };
                        
                        // Use transit-colored text
                        var textPaint = new SKPaint
                        {
                            Color = SKPaints.PaintExfilTransit.Color,
                            IsStroke = false,
                            IsAntialias = true
                        };
                        _canvas.DrawText($"{transit.Description} [Transit] D:{distance:F0}m", new SKPoint(screen.X + r + 3, screen.Y + r + 1), SKTextAlign.Left, font, textPaint);
                    }
                }
            }
        }

        private void DrawExplosives(LocalPlayer localPlayer)
        {
            if (Explosives is null)
                return;

            // Active explosives (grenades, tripwires) are ALWAYS rendered regardless of loot distance settings
            // They are safety-critical and should never be hidden by distance sliders

            foreach (var explosive in Explosives)
            {
                try
                {
                    if (explosive is null || explosive.Position == Vector3.Zero)
                        continue;

                    if (!TryProject(explosive.Position, out var screen, out float scale, localPlayer))
                        continue;

                    // Scale radius with perspective (from TryProject)
                    float r = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);

                    string label;
                    float distance = Vector3.Distance(localPlayer.Position, explosive.Position);
                    
                    if (explosive is Tripwire tripwire && tripwire.IsActive)
                    {
                        _canvas.DrawCircle(screen.X, screen.Y, r, SKPaints.PaintExplosives);
                        label = $"Tripwire D:{distance:F0}m";
                    }
                    else if (explosive is Grenade)
                    {
                        _canvas.DrawCircle(screen.X, screen.Y, r, SKPaints.PaintExplosives);
                        label = $"Grenade D:{distance:F0}m";
                    }
                    else
                    {
                        continue;
                    }

                    // Scale font with perspective
                    float baseFontSize = SKFonts.EspWidgetFont.Size * scale * 0.9f;
                    float fontSize = Math.Clamp(baseFontSize, 8f, 20f);
                    using var font = new SKFont(SKFonts.EspWidgetFont.Typeface, fontSize) { Subpixel = true };
                    var textPaint = new SKPaint
                    {
                        Color = SKPaints.PaintExplosives.Color,
                        IsStroke = false,
                        IsAntialias = true
                    };
                    _canvas.DrawText(label, new SKPoint(screen.X + r + 3, screen.Y + r + 1), SKTextAlign.Left, font, textPaint);
                }
                catch
                {
                    // Skip invalid explosives
                    continue;
                }
            }
        }

        private void DrawStaticContainers(LocalPlayer localPlayer)
        {
            // Controlled by Aimview-specific "Show Containers" checkbox
            if (!App.Config.AimviewWidget.ShowContainers)
                return;

            var containers = Memory.Game?.Loot?.AllLoot?.OfType<StaticLootContainer>();
            if (containers is null)
                return;

            bool selectAll = App.Config.Containers.SelectAll;
            var selected = App.Config.Containers.Selected;
            bool hideSearched = App.Config.Containers.HideSearched;
            float maxRenderDistance = App.Config.AimviewWidget.ContainerDistance;
            bool unlimitedDistance = maxRenderDistance <= 0f; // 0 = unlimited

            foreach (var container in containers)
            {
                var id = container.ID ?? "UNKNOWN";
                if (!selectAll && !selected.ContainsKey(id))
                    continue;

                // Hide searched containers if enabled
                if (hideSearched && container.Searched)
                    continue;

                float distance = Vector3.Distance(localPlayer.Position, container.Position);
                if (!unlimitedDistance && distance > maxRenderDistance)
                    continue;

                if (TryProject(container.Position, out var screen, out float scale, localPlayer))
                {
                    // Scale radius with perspective (from TryProject)
                    float r = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);

                    var paint = SKPaints.PaintContainerLoot;
                    _canvas.DrawCircle(screen.X, screen.Y, r, paint);

                    // Scale font with perspective
                    float baseFontSize = SKFonts.EspWidgetFont.Size * scale * 0.9f;
                    float fontSize = Math.Clamp(baseFontSize, 8f, 20f);
                    using var font = new SKFont(SKFonts.EspWidgetFont.Typeface, fontSize) { Subpixel = true };
                    var textPaint = new SKPaint
                    {
                        Color = SKPaints.PaintContainerLoot.Color,
                        IsStroke = false,
                        IsAntialias = true
                    };
                    _canvas.DrawText(container.Name ?? "Container", new SKPoint(screen.X + r + 3, screen.Y + r + 1), SKTextAlign.Left, font, textPaint);
                }
            }
        }

        private void DrawCorpseMarkers(LocalPlayer localPlayer)
        {
            // ✅ Controlled by Radar "Draw Corpse Markers" checkbox
            if (!App.Config.Loot.ShowCorpseMarkers)
                return;

            var corpses = Memory.Game?.Loot?.AllLoot?.OfType<LootCorpse>();
            if (corpses is null)
                return;

            // Use same distance as Aimview Loot (0 = unlimited)
            float maxRenderDistance = App.Config.UI.AimviewLootRenderDistance;
            bool unlimitedDistance = maxRenderDistance <= 0f;

            foreach (var corpse in corpses)
            {
                float distance = Vector3.Distance(localPlayer.Position, corpse.Position);
                if (!unlimitedDistance && distance > maxRenderDistance)
                    continue;

                if (TryProject(corpse.Position, out var screen, out float scale, localPlayer))
                {
                    // Scale radius with perspective (from TryProject)
                    float r = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);

                    // Get paint based on corpse type
                    var (shapePaint, textColor) = GetCorpsePaints(corpse);
                    _canvas.DrawCircle(screen.X, screen.Y, r, shapePaint);

                    // Scale font with perspective
                    float baseFontSize = SKFonts.EspWidgetFont.Size * scale * 0.9f;
                    float fontSize = Math.Clamp(baseFontSize, 8f, 20f);
                    using var font = new SKFont(SKFonts.EspWidgetFont.Typeface, fontSize) { Subpixel = true };
                    
                    float textY = screen.Y + r + 1;
                    
                    // Draw ALL important item labels ABOVE the name (only for AI corpses, not PMC/human)
                    bool isAICorpse = corpse.Player?.IsAI ?? false;
                    if (isAICorpse)
                    {
                        var importantItems = corpse.GetAllImportantItems().ToList();
                        foreach (var importantItem in importantItems)
                        {
                            // Get the appropriate color based on item type
                            SKColor importantColor;
                            if (importantItem.Type == CorpseImportantItemType.Wishlist)
                            {
                                // Wishlist items use WishlistLoot color (configurable via Color Picker)
                                importantColor = SKPaints.TextWishlistItem.Color;
                            }
                            else if (!string.IsNullOrEmpty(importantItem.CustomFilterColor) && 
                                     SKColor.TryParse(importantItem.CustomFilterColor, out var filterColor))
                            {
                                // Filter items use their custom color
                                importantColor = filterColor;
                            }
                            else
                            {
                                // Fallback to ImportantLoot (ValuableLoot) color
                                importantColor = SKPaints.TextImportantLoot.Color;
                            }
                            
                            var importantPaint = new SKPaint
                            {
                                Color = importantColor,
                                IsStroke = false,
                                IsAntialias = true
                            };
                            _canvas.DrawText(importantItem.Label, new SKPoint(screen.X + r + 3, textY), SKTextAlign.Left, font, importantPaint);
                            textY += fontSize + 2; // Move down for next item or corpse name
                        }
                    }
                    
                    // Draw corpse name with type
                    var textPaint = new SKPaint
                    {
                        Color = textColor,
                        IsStroke = false,
                        IsAntialias = true
                    };
                    _canvas.DrawText($"{corpse.Name} D:{distance:F0}m", new SKPoint(screen.X + r + 3, textY), SKTextAlign.Left, font, textPaint);
                }
            }
        }

        /// <summary>
        /// Gets the appropriate paints based on the corpse's player type.
        /// </summary>
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

        private void DrawPlayersAndAIAsSkeletons(LocalPlayer localPlayer)
        {
            if (!App.Config.AimviewWidget.ShowAI && !App.Config.AimviewWidget.ShowEnemyPlayers)
                return; // Both disabled, skip entirely

            var players = AllPlayers?
                .Where(p => p.IsActive && p.IsAlive && p is not Tarkov.GameWorld.Player.LocalPlayer);

            if (players is null)
                return;

            bool drawHeadCircles = App.Config.AimviewWidget.ShowHeadCircle;

            foreach (var player in players)
            {
                // Filter based on config
                bool isAI = player.IsAI;
                bool isEnemyPlayer = !isAI && player.IsHostile;

                if (isAI && !App.Config.AimviewWidget.ShowAI)
                    continue;
                if (isEnemyPlayer && !App.Config.AimviewWidget.ShowEnemyPlayers)
                    continue;

                float distance = Vector3.Distance(localPlayer.Position, player.Position);
                if (App.Config.UI.MaxDistance > 0 && distance > App.Config.UI.MaxDistance)
                    continue;

                // Check if skeleton exists
                if (player.Skeleton?.BoneTransforms == null)
                    continue;

                var paint = GetPaint(player);

                // Calculate distance-based scale for line thickness
                float distanceScale = Math.Clamp(50f / Math.Max(distance, 5f), 0.5f, 2.5f);

                foreach (var (from, to) in _boneConnections)
                {
                    // Use Skeleton.BoneTransforms directly (same as DeviceAimbot) for fresh positions
                    if (!player.Skeleton.BoneTransforms.TryGetValue(from, out var bone1) ||
                        !player.Skeleton.BoneTransforms.TryGetValue(to, out var bone2))
                        continue;

                    var p1 = bone1.Position;
                    var p2 = bone2.Position;
                    
                    if (p1 == Vector3.Zero || p2 == Vector3.Zero) continue;
                    if (TryProject(p1, out var s1) && TryProject(p2, out var s2))
                    {
                        // Scale line thickness with distance
                        float t = Math.Max(0.5f, 1.5f * distanceScale);
                        paint.StrokeWidth = t;
                        _canvas.DrawLine(s1.X, s1.Y, s2.X, s2.Y, paint);
                    }
                }

                // Draw head circle if enabled
                if (drawHeadCircles && player.Skeleton.BoneTransforms.TryGetValue(Bones.HumanHead, out var headBone))
                {
                    var head = headBone.Position;
                    if (head != Vector3.Zero && !float.IsNaN(head.X) && !float.IsInfinity(head.X))
                    {
                        var headTop = head;
                        headTop.Y += 0.18f; // small offset to estimate head height

                        if (TryProject(head, out var headScreen) && TryProject(headTop, out var headTopScreen))
                        {
                            // Calculate radius based on projected head height
                            var dy = MathF.Abs(headTopScreen.Y - headScreen.Y);
                            float radius = dy * 0.65f;
                            radius = Math.Clamp(radius, 2f, 12f);
                            
                            // Draw circle (not filled, just outline)
                            paint.Style = SKPaintStyle.Stroke;
                            _canvas.DrawCircle(headScreen.X, headScreen.Y, radius, paint);
                            paint.Style = SKPaintStyle.Fill; // Reset to fill for skeleton lines
                        }
                    }
                }
            }
        }

        private void DrawFilteredLoot(LocalPlayer localPlayer)
        {
            if (!App.Config.AimviewWidget.ShowLoot)
                return; // Loot disabled in Aimview

            if (!(App.Config.Loot.Enabled)) return;
            var lootItems = Memory.Game?.Loot?.FilteredLoot;
            if (lootItems is null) return;

            // 0 = unlimited distance
            float maxDistance = App.Config.UI.AimviewLootRenderDistance <= 0
                ? float.MaxValue 
                : App.Config.UI.AimviewLootRenderDistance;

            foreach (var item in lootItems)
            {
                // Skip containers - they're drawn separately in DrawStaticContainers()
                if (item is StaticLootContainer)
                    continue;

                // Skip corpses - they're drawn separately in DrawCorpseMarkers()
                if (item is LootCorpse)
                    continue;

                if (item.IsQuestItem && !App.Config.AimviewWidget.ShowQuestItems)
                    continue;

                float distance = Vector3.Distance(localPlayer.Position, item.Position);
                if (distance > maxDistance)
                    continue;

                if (TryProject(item.Position, out var screen, out float scale, localPlayer))
                {
                    // Scale radius with perspective (from TryProject)
                    float r = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);
                    
                    // Determine paint and text paint based on item properties
                    // Priority: Quest > Wishlist > Category > CustomFilter > Valuable > Default
                    SKPaint paint;
                    SKPaint textPaint;
                    
                    if (item.IsQuestItem)
                    {
                        paint = SKPaints.PaintQuestItem;
                        textPaint = SKPaints.TextQuestItem;
                    }
                    else if (item.IsWishlisted)
                    {
                        // Wishlist items use RED color and override custom filters
                        paint = SKPaints.PaintWishlistItem;
                        textPaint = SKPaints.TextWishlistItem;
                    }
                    else if (item.IsBackpack)
                    {
                        paint = SKPaints.PaintBackpacks;
                        textPaint = SKPaints.TextBackpacks;
                    }
                    else if (item.IsMeds)
                    {
                        paint = SKPaints.PaintMeds;
                        textPaint = SKPaints.TextMeds;
                    }
                    else if (item.IsFood)
                    {
                        paint = SKPaints.PaintFood;
                        textPaint = SKPaints.TextFood;
                    }
                    else
                    {
                        // Check for custom filter color
                        var filterColor = item.CustomFilter?.Color;
                        if (!string.IsNullOrEmpty(filterColor) && SKColor.TryParse(filterColor, out var skColor))
                        {
                            paint = new SKPaint
                            {
                                Color = skColor,
                                StrokeWidth = 0.25f,
                                Style = SKPaintStyle.Fill,
                                IsAntialias = true
                            };
                            textPaint = new SKPaint
                            {
                                Color = skColor,
                                IsStroke = false,
                                IsAntialias = true
                            };
                        }
                        else if (item.IsValuableLoot)
                        {
                            paint = SKPaints.PaintImportantLoot;
                            textPaint = SKPaints.TextImportantLoot;
                        }
                        else
                        {
                            paint = SKPaints.PaintFilteredLoot;
                            textPaint = SKPaints.TextFilteredLoot;
                        }
                    }

                    _canvas.DrawCircle(screen.X, screen.Y, r, paint);

                    // Use GetUILabel() to get consistent label with "!!" prefix for wishlist items
                    var label = item.GetUILabel();
                    label = $"{label} D:{distance:F0}m";
                    
                    // Scale font with perspective
                    float baseFontSize = SKFonts.EspWidgetFont.Size * scale * 0.9f;
                    float fontSize = Math.Clamp(baseFontSize, 8f, 20f);
                    using var font = new SKFont(SKFonts.EspWidgetFont.Typeface, fontSize) { Subpixel = true };
                    _canvas.DrawText(label, new SKPoint(screen.X + r + 3, screen.Y + r + 1), SKTextAlign.Left, font, textPaint);
                }
            }
        }

        private void DrawCrosshair()
        {
            // Draw crosshair at widget center
            var bounds = _bitmap.Info.Rect;
            var center = new SKPoint(bounds.MidX, bounds.MidY);
            _canvas.DrawLine(0, center.Y, _bitmap.Width, center.Y, SKPaints.PaintAimviewWidgetCrosshair);
            _canvas.DrawLine(center.X, 0, center.X, _bitmap.Height, SKPaints.PaintAimviewWidgetCrosshair);
        }

        private void EnsureSurface(SKSize size)
        {
            if (_bitmap != null &&
                _canvas != null &&
                _bitmap.Width == (int)size.Width &&
                _bitmap.Height == (int)size.Height)
                return;

            DisposeSurface();
            AllocateSurface((int)size.Width, (int)size.Height);
        }

        private void AllocateSurface(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return;

            _bitmap = new SKBitmap(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            _canvas = new SKCanvas(_bitmap);
        }

        private void DisposeSurface()
        {
            _canvas?.Dispose();
            _canvas = null;
            _bitmap?.Dispose();
            _bitmap = null;
        }

        public override void SetScaleFactor(float newScale)
        {
            base.SetScaleFactor(newScale);
            // Consolidated strokes
            float std = 1f * newScale;
            SKPaints.PaintAimviewWidgetCrosshair.StrokeWidth = std;
            SKPaints.PaintAimviewWidgetLocalPlayer.StrokeWidth = std;
            SKPaints.PaintAimviewWidgetPMC.StrokeWidth = std;
            SKPaints.PaintAimviewWidgetWatchlist.StrokeWidth = std;
            SKPaints.PaintAimviewWidgetStreamer.StrokeWidth = std;
            SKPaints.PaintAimviewWidgetTeammate.StrokeWidth = std;
            SKPaints.PaintAimviewWidgetBoss.StrokeWidth = std;
            SKPaints.PaintAimviewWidgetScav.StrokeWidth = std;
            SKPaints.PaintAimviewWidgetRaider.StrokeWidth = std;
            SKPaints.PaintAimviewWidgetPScav.StrokeWidth = std;
            SKPaints.PaintAimviewWidgetFocused.StrokeWidth = std;
        }

        public override void Dispose()
        {
            DisposeSurface();
            base.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static SKPaint GetPaint(AbstractPlayer player)
        {
            if (player.IsFocused)
                return SKPaints.PaintAimviewWidgetFocused;
            if (player is LocalPlayer)
                return SKPaints.PaintAimviewWidgetLocalPlayer;

            return player.Type switch
            {
                PlayerType.Teammate => SKPaints.PaintAimviewWidgetTeammate,
                PlayerType.PMC => SKPaints.PaintAimviewWidgetPMC,
                PlayerType.AIScav => SKPaints.PaintAimviewWidgetScav,
                PlayerType.AIRaider => SKPaints.PaintAimviewWidgetRaider,
                PlayerType.AIBoss => SKPaints.PaintAimviewWidgetBoss,
                PlayerType.PScav => SKPaints.PaintAimviewWidgetPScav,
                PlayerType.SpecialPlayer => SKPaints.PaintAimviewWidgetWatchlist,
                PlayerType.Streamer => SKPaints.PaintAimviewWidgetStreamer,
                _ => SKPaints.PaintAimviewWidgetPMC
            };
        }

        // Scale CameraManager coordinates to widget size
        private bool TryProject(in Vector3 world, out SKPoint scr, out float scale, LocalPlayer localPlayer = null)
        {
            scr = default;
            scale = 1f;
            
            if (world == Vector3.Zero)
                return false;
            
            // Get projection from CameraManager (PR #6 version - no depth output)
            if (!CameraManagerNew.WorldToScreen(in world, out var espScreen, false, false))
            {
                return false;
            }

            // Get viewport dimensions from CameraManager
            var viewport = CameraManagerNew.Viewport;
            if (viewport.Width <= 0 || viewport.Height <= 0)
                return false;

            // Calculate relative position in viewport (0.0 to 1.0)
            float relX = espScreen.X / viewport.Width;
            float relY = espScreen.Y / viewport.Height;

            // Scale to widget dimensions
            scr = new SKPoint(
                relX * _bitmap.Width,
                relY * _bitmap.Height
            );

            // Calculate scale based on distance from PLAYER (not camera) for better scaling effect
            Vector3 refPos = localPlayer?.Position ?? CameraManagerNew.CameraPosition;
            float dist = Vector3.Distance(refPos, world);
            
            // ✅ Perspective-based scaling - markers get SMALLER at greater distances (natural view)
            // At close range (5m): scale ~2.0x (larger, more visible)
            // At medium range (10m): scale ~1.0x (normal size)  
            // At far range (30m+): scale ~0.33x (smaller, less obtrusive)
            const float referenceDistance = 10f; // Reference distance for 1.0x scale
            scale = Math.Clamp(referenceDistance / Math.Max(dist, 1f), 0.3f, 3f);
            
            // Check if within widget bounds (allow some tolerance for edge cases)
            const float tolerance = 100f;
            if (scr.X < -tolerance || scr.X > _bitmap.Width + tolerance || 
                scr.Y < -tolerance || scr.Y > _bitmap.Height + tolerance)
            {
                return false;
            }

            return true;
        }

        // Overload without scale for compatibility
        private bool TryProject(in Vector3 world, out SKPoint scr)
        {
            return TryProject(world, out scr, out _, null);
        }
    }
}