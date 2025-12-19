/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 * 
 * Exfil Status Tracking: Credit to Keegi
 */

using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Exits;
using LoneEftDmaRadar.Tarkov.GameWorld.Explosives;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.UI.Skia.Rendering;
using SkiaSharp.Views.WPF;

namespace LoneEftDmaRadar.UI.Skia
{
    public sealed class AimviewWidget : AbstractSKWidget
    {
        private SKBitmap _bitmap;
        private SKCanvas _canvas;

        // Renderers
        private AimviewWorldRenderer _worldRenderer;
        private AimviewLootRenderer _lootRenderer;
        private AimviewPlayerRenderer _playerRenderer;

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

                // Use dedicated renderers
                _worldRenderer.DrawExfils(localPlayer, Exits);
                _worldRenderer.DrawExplosives(localPlayer, Explosives);
                _worldRenderer.DrawStaticContainers(localPlayer, Memory.Game?.Loot?.AllLoot?.OfType<StaticLootContainer>());
                
                _lootRenderer.DrawCorpseMarkers(localPlayer, Memory.Game?.Loot?.AllLoot?.OfType<LootCorpse>());
                _lootRenderer.DrawFilteredLoot(localPlayer, Memory.Game?.Loot?.FilteredLoot);
                
                _playerRenderer.DrawPlayersAndAIAsSkeletons(localPlayer, AllPlayers);
                
                _worldRenderer.DrawCrosshair();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"CRITICAL AIMVIEW WIDGET RENDER ERROR: {ex}");
            }

            _canvas.Flush();
            targetCanvas.DrawBitmap(_bitmap, dest, SKPaints.PaintBitmap);
        }

        #region Surface Management

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

            // Initialize renderers with new surface
            _worldRenderer = new AimviewWorldRenderer(_bitmap, _canvas);
            _lootRenderer = new AimviewLootRenderer(_bitmap, _canvas);
            _playerRenderer = new AimviewPlayerRenderer(_bitmap, _canvas);
        }

        private void DisposeSurface()
        {
            _canvas?.Dispose();
            _canvas = null;
            _bitmap?.Dispose();
            _bitmap = null;
            _worldRenderer = null;
            _lootRenderer = null;
            _playerRenderer = null;
        }

        #endregion

        #region Scale Management

        public override void SetScaleFactor(float newScale)
        {
            base.SetScaleFactor(newScale);
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

        #endregion

        public override void Dispose()
        {
            DisposeSurface();
            base.Dispose();
        }
    }
}