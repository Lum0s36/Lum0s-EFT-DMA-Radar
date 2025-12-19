/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld.Exits;
using LoneEftDmaRadar.Tarkov.GameWorld.Explosives;
using LoneEftDmaRadar.UI.Skia;
using SharpDX.Mathematics.Interop;
using SkiaSharp;
using DxColor = SharpDX.Mathematics.Interop.RawColorBGRA;

namespace LoneEftDmaRadar.UI.ESP.Rendering
{
    /// <summary>
    /// Handles rendering of world elements (exfils, tripwires, grenades) on the ESP overlay.
    /// </summary>
    internal sealed class EspWorldRenderer
    {
        #region Public Methods

        /// <summary>
        /// Renders all exfil points on the ESP overlay.
        /// </summary>
        public void DrawExfils(EspRenderContext context, IReadOnlyCollection<IExitPoint> exits)
        {
            if (exits == null || !App.Config.UI.EspExfils)
                return;

            foreach (var exit in exits)
            {
                DrawExitPoint(context, exit);
            }
        }

        /// <summary>
        /// Renders all tripwires on the ESP overlay.
        /// </summary>
        public void DrawTripwires(EspRenderContext context, IReadOnlyCollection<IExplosiveItem> explosives)
        {
            if (explosives == null || !App.Config.UI.EspTripwires)
                return;

            foreach (var explosive in explosives)
            {
                if (explosive is Tripwire tripwire && tripwire.IsActive)
                {
                    DrawTripwire(context, tripwire);
                }
            }
        }

        /// <summary>
        /// Renders all grenades on the ESP overlay.
        /// </summary>
        public void DrawGrenades(EspRenderContext context, IReadOnlyCollection<IExplosiveItem> explosives)
        {
            if (explosives == null || !App.Config.UI.EspGrenades)
                return;

            foreach (var explosive in explosives)
            {
                if (explosive is Grenade grenade)
                {
                    DrawGrenade(context, grenade);
                }
            }
        }

        #endregion

        #region Private Methods

        private void DrawExitPoint(EspRenderContext context, IExitPoint exit)
        {
            if (exit is Exfil exfil)
            {
                DrawExfil(context, exfil);
            }
            else if (exit is TransitPoint transit)
            {
                DrawTransit(context, transit);
            }
        }

        private void DrawExfil(EspRenderContext context, Exfil exfil)
        {
            // Skip closed exfils
            if (exfil.Status == Exfil.EStatus.Closed)
                return;

            if (!context.WorldToScreenWithScale(exfil.Position, out var screen, out float scale))
                return;

            float distance = context.DistanceTo(exfil.Position);

            // Status-based color
            var dotColor = exfil.Status switch
            {
                Exfil.EStatus.Open => EspColorHelper.GetExfilOpenColor(),
                Exfil.EStatus.Pending => EspColorHelper.GetExfilPendingColor(),
                _ => EspColorHelper.GetExfilOpenColor()
            };
            var textColor = EspColorHelper.GetExfilColor();

            // Status text
            var statusText = exfil.Status switch
            {
                Exfil.EStatus.Pending => " [Pending]",
                _ => ""
            };

            // Draw marker
            float radius = context.GetScaledRadius(scale);
            context.Ctx.DrawCircle(ToRaw(screen), radius, dotColor, true);

            // Draw label
            var textSize = context.GetTextSize(scale);
            context.Ctx.DrawText(
                $"{exfil.Name}{statusText} D:{distance:F0}m",
                screen.X + radius + ESPConstants.TextOffsetFromMarker,
                screen.Y + ESPConstants.TextOffsetFromMarker,
                textColor,
                textSize);
        }

        private void DrawTransit(EspRenderContext context, TransitPoint transit)
        {
            if (!context.WorldToScreenWithScale(transit.Position, out var screen, out float scale))
                return;

            float distance = context.DistanceTo(transit.Position);
            var color = EspColorHelper.GetTransitColor();

            // Draw marker
            float radius = context.GetScaledRadius(scale);
            context.Ctx.DrawCircle(ToRaw(screen), radius, color, true);

            // Draw label
            var textSize = context.GetTextSize(scale);
            context.Ctx.DrawText(
                $"{transit.Description} [Transit] D:{distance:F0}m",
                screen.X + radius + ESPConstants.TextOffsetFromMarker,
                screen.Y + ESPConstants.TextOffsetFromMarker,
                color,
                textSize);
        }

        private void DrawTripwire(EspRenderContext context, Tripwire tripwire)
        {
            if (tripwire.Position == Vector3.Zero)
                return;

            if (!context.WorldToScreenWithScale(tripwire.Position, out var screen, out float scale))
                return;

            float distance = context.DistanceTo(tripwire.Position);
            var color = EspColorHelper.GetTripwireColor();

            // Draw marker
            float radius = context.GetScaledRadius(scale);
            context.Ctx.DrawCircle(ToRaw(screen), radius, color, true);

            // Draw label
            var textSize = context.GetTextSize(scale);
            context.Ctx.DrawText(
                $"Tripwire D:{distance:F0}m",
                screen.X + radius + ESPConstants.TextOffsetFromMarker,
                screen.Y,
                color,
                textSize);
        }

        private void DrawGrenade(EspRenderContext context, Grenade grenade)
        {
            if (grenade.Position == Vector3.Zero)
                return;

            if (!context.WorldToScreenWithScale(grenade.Position, out var screen, out float scale))
                return;

            float distance = context.DistanceTo(grenade.Position);
            var color = EspColorHelper.GetGrenadeColor();

            // Draw marker
            float radius = context.GetScaledRadius(scale);
            context.Ctx.DrawCircle(ToRaw(screen), radius, color, true);

            // Draw label
            var textSize = context.GetTextSize(scale);
            context.Ctx.DrawText(
                $"Grenade D:{distance:F0}m",
                screen.X + radius + ESPConstants.TextOffsetFromMarker,
                screen.Y,
                color,
                textSize);
        }

        #endregion

        #region Utility

        private static RawVector2 ToRaw(SKPoint point) => new(point.X, point.Y);

        #endregion
    }
}
