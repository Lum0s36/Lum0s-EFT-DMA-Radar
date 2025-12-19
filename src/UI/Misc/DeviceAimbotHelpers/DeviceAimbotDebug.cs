/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using System;
using System.Collections.Generic;
using System.Numerics;
using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Misc.Ballistics;
using LoneEftDmaRadar.UI.Skia;
using SkiaSharp;

namespace LoneEftDmaRadar.UI.Misc.DeviceAimbotHelpers
{
    /// <summary>
    /// Handles debug overlay rendering and snapshot generation for DeviceAimbot.
    /// </summary>
    public sealed class DeviceAimbotDebug
    {
        #region Static Paints

        private static readonly SKPaint s_bgPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, DeviceAimbotConstants.DebugBackgroundAlpha),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        
        private static readonly SKPaint s_textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };
        
        private static readonly SKPaint s_headerPaint = new SKPaint
        {
            Color = SKColors.Yellow,
            IsAntialias = true
        };
        
        private static readonly SKPaint s_shadowPaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = DeviceAimbotConstants.DebugShadowStrokeWidth
        };
        
        private static readonly SKFont s_monoFont = SKFonts.InfoWidgetFont;

        #endregion

        private readonly List<string> _debugLines = new(DeviceAimbotConstants.DebugLinesCapacity);
        private static DeviceAimbotConfig Config => App.Config.Device;

        /// <summary>
        /// Draws debug information on the ESP/radar overlay.
        /// </summary>
        public void DrawDebug(
            SKCanvas canvas,
            LocalPlayer localPlayer,
            MemDMA memory,
            string debugStatus,
            bool isEngaged,
            AbstractPlayer lockedTarget,
            TargetingStats stats,
            BallisticsInfo lastBallistics,
            bool hasFireport,
            Vector3 lastFireportPos)
        {
            try
            {
                var lines = _debugLines;
                lines.Clear();

                // Header
                lines.Add("=== DeviceAimbot AIMBOT DEBUG ===");
                lines.Add($"Status:       {debugStatus}");
                lines.Add($"Key State:    {(isEngaged ? "ENGAGED" : "Idle")}");
                bool devConnected = Device.connected || DeviceNetController.Connected;
                lines.Add($"Device:       {(devConnected ? "Connected" : "Disconnected")}");
                lines.Add($"Enabled:      {(Config.Enabled ? "TRUE" : "FALSE")}");
                lines.Add($"InRaid:       {memory.InRaid}");
                lines.Add("");

                // LocalPlayer / Firearm / Fireport info
                if (localPlayer != null)
                {
                    lines.Add($"LocalPlayer:  OK @ {localPlayer.Position}");
                    lines.Add($"FirearmMgr:   {(localPlayer.FirearmManager != null ? "OK" : "NULL")}");
                }
                else
                {
                    lines.Add("LocalPlayer:  NULL");
                    lines.Add("FirearmMgr:   n/a");
                }

                if (hasFireport)
                {
                    lines.Add($"FireportPos:  {lastFireportPos}");
                }
                else
                {
                    lines.Add("FireportPos:  (no valid fireport)");
                }

                lines.Add("");

                // Per-stage filter stats
                lines.Add("Players (this scan):");
                lines.Add($"  Total:      {stats.TotalPlayers}");
                lines.Add($"  Type OK:    {stats.EligibleType}");
                lines.Add($"  InDist:     {stats.WithinDistance}");
                lines.Add($"  Skeleton:   {stats.HaveSkeleton}");
                lines.Add($"  W2S OK:     {stats.W2SPassed}");
                lines.Add($"  Candidates: {stats.CandidateCount}");
                lines.Add("");

                // Target info / FOV diagnostics
                lines.Add($"Config FOV:   {Config.FOV:F1} (pixels from screen center)");
                lines.Add($"TargetValid:  {stats.LastIsTargetValid}");

                if (lockedTarget != null && localPlayer != null)
                {
                    var dist = Vector3.Distance(localPlayer.Position, lockedTarget.Position);
                    lines.Add($"Locked Target: {lockedTarget.Name} [{lockedTarget.Type}]");
                    lines.Add($"  Distance:   {dist:F1}m");
                    if (!float.IsNaN(stats.LastLockedTargetFov) && !float.IsInfinity(stats.LastLockedTargetFov))
                        lines.Add($"  FOVDist:    {stats.LastLockedTargetFov:F1}");
                    else
                        lines.Add("  FOVDist:    n/a");

                    lines.Add($"  TargetBone: {Config.TargetBone}");

                    if (lockedTarget is ObservedPlayer obs)
                    {
                        lines.Add($"  Health:     {obs.HealthStatus}");
                    }
                }
                else
                {
                    lines.Add("Locked Target: None");
                }

                lines.Add("");

                // Ballistics Info
                if (lastBallistics != null && lastBallistics.IsAmmoValid)
                {
                    lines.Add("Ballistics:");
                    lines.Add($"  BulletSpeed: {(lastBallistics.BulletSpeed):F1} m/s");
                    lines.Add($"  Mass:        {lastBallistics.BulletMassGrams:F2} g");
                    lines.Add($"  BC:          {lastBallistics.BallisticCoefficient:F3}");
                    lines.Add($"  Prediction:  {(Config.EnablePrediction ? "ON" : "OFF")}");
                }
                else
                {
                    lines.Add("Ballistics:   No / invalid ammo");
                }

                lines.Add("");

                // Settings / filters
                lines.Add("Settings:");
                lines.Add($"  MaxDist:    {Config.MaxDistance:F0}m");
                lines.Add($"  Targeting:  {Config.Targeting}");
                lines.Add("");
                lines.Add("Target Filters:");
                lines.Add($"  PMC:    {Config.TargetPMC}   PScav: {Config.TargetPlayerScav}");
                lines.Add($"  AI:     {Config.TargetAIScav}   Boss:  {Config.TargetBoss}   Raider: {Config.TargetRaider}");

                lines.Add("");
                lines.Add("No Recoil:");
                lines.Add($"  Enabled:    {(App.Config.MemWrites.NoRecoilEnabled ? "ON" : "OFF")}");
                if (App.Config.MemWrites.NoRecoilEnabled)
                {
                    lines.Add($"  Recoil:     {App.Config.MemWrites.NoRecoilAmount:F0}%");
                    lines.Add($"  Sway:       {App.Config.MemWrites.NoSwayAmount:F0}%");
                }

                DrawLines(canvas, lines);
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[DeviceAimbot] DrawDebug error: {ex}");
            }
        }

        /// <summary>
        /// Returns the latest debug snapshot for UI/ESP overlays.
        /// </summary>
        public DeviceAimbotDebugSnapshot GetDebugSnapshot(
            MemDMA memory,
            string debugStatus,
            bool isEngaged,
            AbstractPlayer lockedTarget,
            TargetingStats stats,
            BallisticsInfo lastBallistics,
            bool hasFireport,
            Vector3 lastFireportPos)
        {
            try
            {
                var localPlayer = memory.LocalPlayer;
                float? distanceToTarget = null;
                if (localPlayer != null && lockedTarget != null)
                    distanceToTarget = Vector3.Distance(localPlayer.Position, lockedTarget.Position);

                return new DeviceAimbotDebugSnapshot
                {
                    Status = debugStatus,
                    KeyEngaged = isEngaged,
                    Enabled = Config.Enabled,
                    DeviceConnected = Device.connected || DeviceNetController.Connected,
                    InRaid = memory.InRaid,
                    CandidateTotal = stats.TotalPlayers,
                    CandidateTypeOk = stats.EligibleType,
                    CandidateInDistance = stats.WithinDistance,
                    CandidateWithSkeleton = stats.HaveSkeleton,
                    CandidateW2S = stats.W2SPassed,
                    CandidateCount = stats.CandidateCount,
                    ConfigFov = Config.FOV,
                    ConfigMaxDistance = Config.MaxDistance,
                    TargetingMode = Config.Targeting,
                    TargetBone = Config.TargetBone,
                    PredictionEnabled = Config.EnablePrediction,
                    TargetValid = stats.LastIsTargetValid,
                    LockedTargetName = lockedTarget?.Name,
                    LockedTargetType = lockedTarget?.Type,
                    LockedTargetDistance = distanceToTarget,
                    LockedTargetFov = stats.LastLockedTargetFov,
                    HasFireport = hasFireport,
                    FireportPosition = hasFireport ? lastFireportPos : (Vector3?)null,
                    BallisticsValid = lastBallistics?.IsAmmoValid ?? false,
                    BulletSpeed = lastBallistics?.BulletSpeed
                };
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[DeviceAimbot] GetDebugSnapshot error: {ex}");
                return null;
            }
        }

        private void DrawLines(SKCanvas canvas, List<string> lines)
        {
            float x = DeviceAimbotConstants.DebugOverlayX;
            float y = DeviceAimbotConstants.DebugOverlayY;
            float lineHeight = DeviceAimbotConstants.DebugLineHeight;

            // Background size
            float maxWidth = 0;
            foreach (var line in lines)
            {
                var width = s_monoFont.MeasureText(line, out _);
                if (width > maxWidth) maxWidth = width;
            }

            canvas.DrawRect(
                x - DeviceAimbotConstants.DebugOverlayPadding,
                y - DeviceAimbotConstants.DebugOverlayExtraHeight,
                maxWidth + DeviceAimbotConstants.DebugOverlayExtraWidth,
                lines.Count * lineHeight + DeviceAimbotConstants.DebugOverlayExtraHeight,
                s_bgPaint);

            // Text with shadow
            foreach (var line in lines)
            {
                var paint = line.StartsWith("===") ||
                            line == "Ballistics:" ||
                            line == "Settings:" ||
                            line == "Target Filters:" ||
                            line == "Players (this scan):"
                    ? s_headerPaint
                    : s_textPaint;

                canvas.DrawText(line, x + DeviceAimbotConstants.DebugShadowOffset, y + DeviceAimbotConstants.DebugShadowOffset, SKTextAlign.Left, s_monoFont, s_shadowPaint);
                canvas.DrawText(line, x, y, SKTextAlign.Left, s_monoFont, paint);
                y += lineHeight;
            }
        }
    }

    /// <summary>
    /// Snapshot of DeviceAimbot state for UI display.
    /// </summary>
    public sealed class DeviceAimbotDebugSnapshot
    {
        public string Status { get; set; }
        public bool KeyEngaged { get; set; }
        public bool Enabled { get; set; }
        public bool DeviceConnected { get; set; }
        public bool InRaid { get; set; }

        public int CandidateTotal { get; set; }
        public int CandidateTypeOk { get; set; }
        public int CandidateInDistance { get; set; }
        public int CandidateWithSkeleton { get; set; }
        public int CandidateW2S { get; set; }
        public int CandidateCount { get; set; }

        public float ConfigFov { get; set; }
        public float ConfigMaxDistance { get; set; }
        public DeviceAimbotConfig.TargetingMode TargetingMode { get; set; }
        public Bones TargetBone { get; set; }
        public bool PredictionEnabled { get; set; }

        public bool TargetValid { get; set; }
        public string LockedTargetName { get; set; }
        public PlayerType? LockedTargetType { get; set; }
        public float? LockedTargetDistance { get; set; }
        public float LockedTargetFov { get; set; }

        public bool HasFireport { get; set; }
        public Vector3? FireportPosition { get; set; }

        public bool BallisticsValid { get; set; }
        public float? BulletSpeed { get; set; }
    }
}
