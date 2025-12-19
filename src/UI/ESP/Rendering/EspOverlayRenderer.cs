/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Misc;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using DxColor = SharpDX.Mathematics.Interop.RawColorBGRA;

namespace LoneEftDmaRadar.UI.ESP.Rendering
{
    /// <summary>
    /// Handles rendering of overlay elements (crosshair, FPS, notifications, debug info) on the ESP.
    /// </summary>
    internal sealed class EspOverlayRenderer
    {
        #region Fields

        private readonly System.Diagnostics.Stopwatch _notificationTimer = new();
        private string _notificationMessage = string.Empty;

        #endregion

        #region Public Methods

        /// <summary>
        /// Draws the crosshair overlay.
        /// </summary>
        public void DrawCrosshair(EspRenderContext context, float strokeWidth)
        {
            if (!App.Config.UI.EspCrosshair)
                return;

            float centerX = context.ScreenWidth / 2f;
            float centerY = context.ScreenHeight / 2f;
            float length = MathF.Max(ESPConstants.MinCrosshairLength, App.Config.UI.EspCrosshairLength);

            var color = EspColorHelper.GetCrosshairColor();

            context.Ctx.DrawLine(
                new RawVector2(centerX - length, centerY),
                new RawVector2(centerX + length, centerY),
                color,
                strokeWidth);

            context.Ctx.DrawLine(
                new RawVector2(centerX, centerY - length),
                new RawVector2(centerX, centerY + length),
                color,
                strokeWidth);
        }

        /// <summary>
        /// Draws the FPS counter.
        /// </summary>
        public void DrawFPS(EspRenderContext context, int fps)
        {
            context.Ctx.DrawText(
                $"FPS: {fps}",
                ESPConstants.FpsDisplayX,
                ESPConstants.FpsDisplayY,
                EspColorHelper.White,
                DxTextSize.Small);
        }

        /// <summary>
        /// Draws "ESP Hidden" message.
        /// </summary>
        public void DrawHiddenMessage(EspRenderContext context)
        {
            context.Ctx.DrawText(
                "ESP Hidden",
                context.ScreenWidth / 2f,
                context.ScreenHeight / 2f,
                EspColorHelper.White,
                DxTextSize.Large,
                centerX: true,
                centerY: true);
        }

        /// <summary>
        /// Shows a notification message.
        /// </summary>
        public void ShowNotification(string message)
        {
            _notificationMessage = message;
            _notificationTimer.Restart();
        }

        /// <summary>
        /// Draws the current notification (if any).
        /// </summary>
        public void DrawNotification(EspRenderContext context)
        {
            if (string.IsNullOrEmpty(_notificationMessage) || !_notificationTimer.IsRunning)
                return;

            long elapsedMs = _notificationTimer.ElapsedMilliseconds;
            if (elapsedMs > ESPConstants.NotificationDurationMs)
            {
                _notificationMessage = string.Empty;
                _notificationTimer.Stop();
                return;
            }

            // Calculate fade opacity
            float opacity = 1.0f;
            if (elapsedMs < ESPConstants.NotificationFadeInMs)
            {
                opacity = elapsedMs / (float)ESPConstants.NotificationFadeInMs;
            }
            else if (elapsedMs > ESPConstants.NotificationDurationMs - ESPConstants.NotificationFadeOutMs)
            {
                float fadeOutStart = ESPConstants.NotificationDurationMs - ESPConstants.NotificationFadeOutMs;
                opacity = 1.0f - ((elapsedMs - fadeOutStart) / (float)ESPConstants.NotificationFadeOutMs);
            }

            opacity = Math.Clamp(opacity, 0.0f, 1.0f);
            byte alpha = (byte)(opacity * 255);

            // Measure text
            var textBounds = context.Ctx.MeasureText(_notificationMessage, DxTextSize.Large);
            int textWidth = Math.Max(1, textBounds.Right - textBounds.Left);
            int textHeight = Math.Max(1, textBounds.Bottom - textBounds.Top);

            // Position (bottom-right)
            float x = context.ScreenWidth - textWidth - ESPConstants.NotificationPadding;
            float y = context.ScreenHeight - textHeight - ESPConstants.NotificationPadding;

            // Background
            float bgX = x - ESPConstants.NotificationBgPadding;
            float bgY = y - ESPConstants.NotificationBgPadding;
            float bgWidth = textWidth + ESPConstants.NotificationBgPadding * 2;
            float bgHeight = textHeight + ESPConstants.NotificationBgPadding * 2;

            var bgColor = new DxColor(0, 0, 0, (byte)(alpha * 0.7f));
            context.Ctx.DrawFilledRect(new RectangleF(bgX, bgY, bgWidth, bgHeight), bgColor);

            // Text
            var textColor = new DxColor(255, 255, 255, alpha);
            context.Ctx.DrawText(_notificationMessage, x, y, textColor, DxTextSize.Large);
        }

        /// <summary>
        /// Draws the nearest player info at the bottom center.
        /// </summary>
        public void DrawNearestPlayerInfo(EspRenderContext context, IReadOnlyCollection<AbstractPlayer> allPlayers)
        {
            if (!App.Config.UI.EspNearestPlayerInfo || context.LocalPlayer == null)
                return;

            var nearestPlayer = FindNearestPlayer(context, allPlayers);
            if (nearestPlayer == null)
                return;

            float distance = context.DistanceTo(nearestPlayer.Position);

            // Build info text
            var parts = new List<string>();

            // Name
            parts.Add(nearestPlayer.Name ?? "Unknown");

            // Distance
            parts.Add($"{distance:F1}m");

            // Type
            string typeText = nearestPlayer.Type switch
            {
                PlayerType.PMC => "PMC",
                PlayerType.PScav => "PScav",
                PlayerType.AIScav => "Scav",
                PlayerType.AIRaider => "Raider",
                PlayerType.AIBoss => "Boss",
                PlayerType.SpecialPlayer => "Special",
                PlayerType.Streamer => "Streamer",
                _ => nearestPlayer.Type.ToString()
            };
            parts.Add(typeText);

            // Health
            if (nearestPlayer is ObservedPlayer observed && observed.HealthStatus != Enums.ETagStatus.Healthy)
            {
                parts.Add(observed.HealthStatus.ToString());
            }

            // Faction
            if (nearestPlayer.IsPmc)
            {
                parts.Add(nearestPlayer.PlayerSide.ToString());
            }

            // Group
            if (nearestPlayer.GroupID >= 0 && nearestPlayer.IsPmc && !nearestPlayer.IsAI)
            {
                parts.Add($"G:{nearestPlayer.GroupID}");
            }

            string text = string.Join(" | ", parts);

            // Position at bottom center
            float textY = context.ScreenHeight - ESPConstants.NearestPlayerInfoPadding;

            // Get color based on player type
            var color = GetNearestPlayerColor(nearestPlayer);

            context.Ctx.DrawText(text, context.ScreenWidth / 2f, textY, color, DxTextSize.Large, centerX: true);
        }

        /// <summary>
        /// Draws the DeviceAimbot target line.
        /// </summary>
        public void DrawDeviceAimbotTargetLine(EspRenderContext context)
        {
            var deviceAimbot = MemDMA.DeviceAimbot;
            if (deviceAimbot?.LockedTarget is not { } target)
                return;

            var headPos = target.GetBonePos(Bones.HumanHead);
            if (!context.WorldToScreen(headPos, out var screen))
                return;

            var center = new RawVector2(context.ScreenWidth / 2f, context.ScreenHeight / 2f);
            bool engaged = deviceAimbot.IsEngaged;
            var color = engaged ? EspColorHelper.DeviceAimbotEngagedColor : EspColorHelper.DeviceAimbotIdleColor;

            context.Ctx.DrawLine(center, new RawVector2(screen.X, screen.Y), color, ESPConstants.DeviceAimbotLineThickness);
        }

        /// <summary>
        /// Draws the DeviceAimbot FOV circle.
        /// </summary>
        public void DrawDeviceAimbotFovCircle(EspRenderContext context)
        {
            var cfg = App.Config.Device;
            if (!cfg.Enabled || !cfg.ShowFovCircle || cfg.FOV <= 0)
                return;

            float radius = Math.Clamp(cfg.FOV, ESPConstants.MinFovCircleRadius, Math.Min(context.ScreenWidth, context.ScreenHeight));
            bool engaged = MemDMA.DeviceAimbot?.IsEngaged == true;

            var colorStr = engaged ? cfg.FovCircleColorEngaged : cfg.FovCircleColorIdle;
            var color = EspColorHelper.ToColor(EspColorHelper.ColorFromHex(colorStr));

            context.Ctx.DrawCircle(
                new RawVector2(context.ScreenWidth / 2f, context.ScreenHeight / 2f),
                radius,
                color,
                filled: false);
        }

        /// <summary>
        /// Draws the DeviceAimbot debug overlay.
        /// </summary>
        public void DrawDeviceAimbotDebugOverlay(EspRenderContext context)
        {
            if (!App.Config.Device.ShowDebug)
                return;

            var snapshot = MemDMA.DeviceAimbot?.GetDebugSnapshot();

            var lines = snapshot == null
                ? new[] { "Device Aimbot: no data" }
                : new[]
                {
                    "=== Device Aimbot ===",
                    $"Status: {snapshot.Status}",
                    $"Key: {(snapshot.KeyEngaged ? "ENGAGED" : "Idle")} | Enabled: {snapshot.Enabled} | Device: {(snapshot.DeviceConnected ? "Connected" : "Disconnected")}",
                    $"InRaid: {snapshot.InRaid} | FOV: {snapshot.ConfigFov:F0}px | MaxDist: {snapshot.ConfigMaxDistance:F0}m | Mode: {snapshot.TargetingMode}",
                    $"Candidates t:{snapshot.CandidateTotal} type:{snapshot.CandidateTypeOk} dist:{snapshot.CandidateInDistance} skel:{snapshot.CandidateWithSkeleton} w2s:{snapshot.CandidateW2S} final:{snapshot.CandidateCount}",
                    $"Target: {(snapshot.LockedTargetName ?? "None")} [{snapshot.LockedTargetType?.ToString() ?? "-"}] valid={snapshot.TargetValid}",
                    snapshot.LockedTargetDistance.HasValue ? $"  Dist {snapshot.LockedTargetDistance.Value:F1}m | FOV {(float.IsNaN(snapshot.LockedTargetFov) ? "n/a" : snapshot.LockedTargetFov.ToString("F1"))} | Bone {snapshot.TargetBone}" : string.Empty,
                    $"Fireport: {(snapshot.HasFireport ? snapshot.FireportPosition?.ToString() : "None")}",
                    $"Ballistics: {(snapshot.BallisticsValid ? $"OK (Speed {(snapshot.BulletSpeed.HasValue ? snapshot.BulletSpeed.Value.ToString("F1") : "?")} m/s, Predict {(snapshot.PredictionEnabled ? "ON" : "OFF")})" : "Invalid/None")}"
                }.Where(l => !string.IsNullOrEmpty(l)).ToArray();

            float x = ESPConstants.DebugOverlayX;
            float y = ESPConstants.DebugOverlayY;

            foreach (var line in lines)
            {
                context.Ctx.DrawText(line, x, y, EspColorHelper.White, DxTextSize.Small);
                y += ESPConstants.DebugOverlayLineHeight;
            }
        }

        #endregion

        #region Private Methods

        private AbstractPlayer FindNearestPlayer(EspRenderContext context, IReadOnlyCollection<AbstractPlayer> allPlayers)
        {
            if (allPlayers == null)
                return null;

            AbstractPlayer nearestPMC = null;
            AbstractPlayer nearestOtherPlayer = null;
            AbstractPlayer nearestAI = null;
            float nearestPMCDistance = float.MaxValue;
            float nearestOtherPlayerDistance = float.MaxValue;
            float nearestAIDistance = float.MaxValue;

            foreach (var player in allPlayers)
            {
                if (player == null || player == context.LocalPlayer || !player.IsAlive || !player.IsActive)
                    continue;

                if (player.Type == PlayerType.Teammate)
                    continue;

                var playerPos = player.Position;
                if (playerPos == Vector3.Zero ||
                    float.IsNaN(playerPos.X) || float.IsInfinity(playerPos.X))
                    continue;

                float distance = context.DistanceTo(playerPos);

                // Priority: PMC > Other human > AI
                if (player.Type == PlayerType.PMC && distance < nearestPMCDistance)
                {
                    nearestPMC = player;
                    nearestPMCDistance = distance;
                }
                else if (player.IsHuman && (player.Type == PlayerType.PScav ||
                                            player.Type == PlayerType.SpecialPlayer ||
                                            player.Type == PlayerType.Streamer) &&
                         distance < nearestOtherPlayerDistance)
                {
                    nearestOtherPlayer = player;
                    nearestOtherPlayerDistance = distance;
                }
                else if (player.IsAI && distance < nearestAIDistance)
                {
                    nearestAI = player;
                    nearestAIDistance = distance;
                }
            }

            return nearestPMC ?? nearestOtherPlayer ?? nearestAI;
        }

        private DxColor GetNearestPlayerColor(AbstractPlayer player)
        {
            return player.Type switch
            {
                PlayerType.PMC => EspColorHelper.GetPlayerColor(),
                PlayerType.PScav => EspColorHelper.GetPlayerScavColor(),
                PlayerType.AIBoss => EspColorHelper.GetBossColor(),
                PlayerType.AIRaider => EspColorHelper.GetRaiderColor(),
                PlayerType.AIScav => EspColorHelper.GetAIColor(),
                PlayerType.SpecialPlayer => EspColorHelper.ToColor(UI.Skia.SKPaints.PaintAimviewWidgetWatchlist),
                PlayerType.Streamer => EspColorHelper.ToColor(UI.Skia.SKPaints.PaintAimviewWidgetStreamer),
                _ => EspColorHelper.White
            };
        }

        #endregion
    }
}
