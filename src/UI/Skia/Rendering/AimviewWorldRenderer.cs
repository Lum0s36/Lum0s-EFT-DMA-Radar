/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Tarkov.GameWorld.Exits;
using LoneEftDmaRadar.Tarkov.GameWorld.Explosives;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Quests;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using SkiaSharp;
using CameraManagerNew = LoneEftDmaRadar.Tarkov.GameWorld.Camera.CameraManager;

namespace LoneEftDmaRadar.UI.Skia.Rendering
{
    /// <summary>
    /// Handles rendering of world elements (exfils, explosives, containers, quest locations) in the Aimview widget.
    /// </summary>
    public sealed class AimviewWorldRenderer
    {
        private readonly SKBitmap _bitmap;
        private readonly SKCanvas _canvas;

        public AimviewWorldRenderer(SKBitmap bitmap, SKCanvas canvas)
        {
            _bitmap = bitmap;
            _canvas = canvas;
        }

        public void DrawExfils(LocalPlayer localPlayer, IReadOnlyCollection<IExitPoint> exits)
        {
            if (!App.Config.AimviewWidget.ShowExfils || exits is null)
                return;

            foreach (var exit in exits)
            {
                if (exit is Exfil exfil)
                {
                    DrawExfil(localPlayer, exfil);
                }
                else if (exit is TransitPoint transit)
                {
                    DrawTransitPoint(localPlayer, transit);
                }
            }
        }

        private void DrawExfil(LocalPlayer localPlayer, Exfil exfil)
        {
            if (exfil.Status == Exfil.EStatus.Closed)
                return;

            if (!TryProject(exfil.Position, out var screen, out float scale, localPlayer))
                return;

            var paint = exfil.Status switch
            {
                Exfil.EStatus.Open => SKPaints.PaintExfilOpen,
                Exfil.EStatus.Pending => SKPaints.PaintExfilPending,
                _ => SKPaints.PaintExfilOpen
            };

            float distance = Vector3.Distance(localPlayer.Position, exfil.Position);
            float r = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);

            _canvas.DrawCircle(screen.X, screen.Y, r, paint);

            var statusText = exfil.Status == Exfil.EStatus.Pending ? " [Pending]" : "";
            DrawLabel($"{exfil.Name}{statusText} D:{distance:F0}m", screen, r, scale, SKPaints.TextExfil);
        }

        private void DrawTransitPoint(LocalPlayer localPlayer, TransitPoint transit)
        {
            if (!TryProject(transit.Position, out var screen, out float scale, localPlayer))
                return;

            float distance = Vector3.Distance(localPlayer.Position, transit.Position);
            float r = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);

            _canvas.DrawCircle(screen.X, screen.Y, r, SKPaints.PaintExfilTransit);

            using var textPaint = new SKPaint
            {
                Color = SKPaints.PaintExfilTransit.Color,
                IsStroke = false,
                IsAntialias = true
            };
            DrawLabel($"{transit.Description} [Transit] D:{distance:F0}m", screen, r, scale, textPaint);
        }

        public void DrawExplosives(LocalPlayer localPlayer, IReadOnlyCollection<IExplosiveItem> explosives)
        {
            if (explosives is null)
                return;

            foreach (var explosive in explosives)
            {
                try
                {
                    if (explosive is null || explosive.Position == Vector3.Zero)
                        continue;

                    if (!TryProject(explosive.Position, out var screen, out float scale, localPlayer))
                        continue;

                    float r = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);
                    float distance = Vector3.Distance(localPlayer.Position, explosive.Position);

                    string label;
                    if (explosive is Tripwire tripwire && tripwire.IsActive)
                    {
                        label = $"Tripwire D:{distance:F0}m";
                    }
                    else if (explosive is Grenade)
                    {
                        label = $"Grenade D:{distance:F0}m";
                    }
                    else
                    {
                        continue;
                    }

                    _canvas.DrawCircle(screen.X, screen.Y, r, SKPaints.PaintExplosives);

                    using var textPaint = new SKPaint
                    {
                        Color = SKPaints.PaintExplosives.Color,
                        IsStroke = false,
                        IsAntialias = true
                    };
                    DrawLabel(label, screen, r, scale, textPaint);
                }
                catch
                {
                    continue;
                }
            }
        }

        public void DrawStaticContainers(LocalPlayer localPlayer, IEnumerable<StaticLootContainer> containers)
        {
            if (!App.Config.AimviewWidget.ShowContainers || containers is null)
                return;

            bool selectAll = App.Config.Containers.SelectAll;
            var selected = App.Config.Containers.Selected;
            bool hideSearched = App.Config.Containers.HideSearched;
            float maxRenderDistance = App.Config.AimviewWidget.ContainerDistance;
            bool unlimitedDistance = maxRenderDistance <= 0f;

            foreach (var container in containers)
            {
                var id = container.ID ?? "UNKNOWN";
                if (!selectAll && !selected.ContainsKey(id))
                    continue;

                if (hideSearched && container.Searched)
                    continue;

                float distance = Vector3.Distance(localPlayer.Position, container.Position);
                if (!unlimitedDistance && distance > maxRenderDistance)
                    continue;

                if (!TryProject(container.Position, out var screen, out float scale, localPlayer))
                    continue;

                float r = Math.Clamp(3f * App.Config.UI.UIScale * scale, 2f, 15f);
                _canvas.DrawCircle(screen.X, screen.Y, r, SKPaints.PaintContainerLoot);

                using var textPaint = new SKPaint
                {
                    Color = SKPaints.PaintContainerLoot.Color,
                    IsStroke = false,
                    IsAntialias = true
                };
                DrawLabel(container.Name ?? "Container", screen, r, scale, textPaint);
            }
        }

        public void DrawQuestLocations(LocalPlayer localPlayer, IReadOnlyDictionary<string, QuestLocation> locations)
        {
            if (!App.Config.AimviewWidget.ShowQuestLocations || locations is null || locations.Count == 0)
                return;

            foreach (var location in locations.Values)
            {
                try
                {
                    if (!TryProject(location.Position, out var screen, out float scale, localPlayer))
                        continue;

                    float distance = Vector3.Distance(localPlayer.Position, location.Position);
                    float r = Math.Clamp(4f * App.Config.UI.UIScale * scale, 2f, 15f);

                    // Draw quest zone marker (square shape to differentiate from other markers)
                    var rect = new SKRect(screen.X - r, screen.Y - r, screen.X + r, screen.Y + r);
                    _canvas.DrawRect(rect, SKPaints.PaintQuestZone);

                    // Draw label with quest info
                    string label = $"{location.ActionText} D:{distance:F0}m";
                    using var textPaint = new SKPaint
                    {
                        Color = SKPaints.PaintQuestZone.Color,
                        IsStroke = false,
                        IsAntialias = true
                    };
                    DrawLabel(label, screen, r, scale, textPaint);
                }
                catch
                {
                    continue;
                }
            }
        }

        public void DrawCrosshair()
        {
            var bounds = _bitmap.Info.Rect;
            var center = new SKPoint(bounds.MidX, bounds.MidY);
            _canvas.DrawLine(0, center.Y, _bitmap.Width, center.Y, SKPaints.PaintAimviewWidgetCrosshair);
            _canvas.DrawLine(center.X, 0, center.X, _bitmap.Height, SKPaints.PaintAimviewWidgetCrosshair);
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
