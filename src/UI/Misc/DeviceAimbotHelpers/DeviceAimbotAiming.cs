/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using System;
using System.Numerics;
using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.UI.Misc.Ballistics;
using CameraManagerNew = LoneEftDmaRadar.Tarkov.GameWorld.Camera.CameraManager;

namespace LoneEftDmaRadar.UI.Misc.DeviceAimbotHelpers
{
    /// <summary>
    /// Handles aiming logic for DeviceAimbot (mouse movement and memory aim).
    /// </summary>
    public sealed class DeviceAimbotAiming
    {
        private static DeviceAimbotConfig Config => App.Config.Device;

        /// <summary>
        /// Sends mouse movement via device.
        /// </summary>
        public void SendDeviceMove(int dx, int dy)
        {
            if (Config.UseKmBoxNet && DeviceNetController.Connected)
            {
                DeviceNetController.Move(dx, dy);
                return;
            }

            Device.move(dx, dy);
        }

        /// <summary>
        /// Aims at the target using either device movement or memory aim.
        /// </summary>
        public void AimAtTarget(
            LocalPlayer localPlayer,
            AbstractPlayer target,
            Vector3 targetPos,
            bool useMemoryAim)
        {
            if (useMemoryAim)
            {
                ApplyMemoryAim(localPlayer, targetPos);
                DebugLogger.LogDebug($"[DeviceAimbot] Using MemoryAim for target {target.Name}");
                return;
            }

            // Original device aiming
            ApplyDeviceAim(target, targetPos);
        }

        /// <summary>
        /// Applies device-based aiming (mouse movement).
        /// </summary>
        private void ApplyDeviceAim(AbstractPlayer target, Vector3 targetPos)
        {
            // Convert to screen space
            if (!CameraManagerNew.WorldToScreen(ref targetPos, out var screenPos, false))
                return;

            // Calculate delta from center
            var center = CameraManagerNew.ViewportCenter;
            float deltaX = screenPos.X - center.X;
            float deltaY = screenPos.Y - center.Y;

            // Apply smoothing (>=1). Higher values = slower movement.
            float smooth = Math.Max(1f, Config.Smoothing);
            deltaX /= smooth;
            deltaY /= smooth;

            // Convert to mouse movement
            int moveX = (int)Math.Round(deltaX);
            int moveY = (int)Math.Round(deltaY);

            // Ensure at least 1 pixel step when delta exists
            if (moveX == 0 && Math.Abs(deltaX) > 0.001f)
                moveX = Math.Sign(deltaX);
            if (moveY == 0 && Math.Abs(deltaY) > 0.001f)
                moveY = Math.Sign(deltaY);

            // Apply movement
            if (moveX != 0 || moveY != 0)
            {
                SendDeviceMove(moveX, moveY);
                DebugLogger.LogDebug($"[DeviceAimbot] Aiming at target {target.Name}: Move({moveX}, {moveY})");
            }
        }

        /// <summary>
        /// Applies memory-based silent aim.
        /// </summary>
        private void ApplyMemoryAim(LocalPlayer localPlayer, Vector3 targetPosition)
        {
            try
            {
                var firearmManager = localPlayer.FirearmManager;
                if (firearmManager == null)
                    return;

                var fireportPos = firearmManager.FireportPosition;
                if (!fireportPos.HasValue || fireportPos.Value == Vector3.Zero)
                    return;

                var fpPos = fireportPos.Value;

                // 1) Desired angle
                Vector2 aimAngle = CalcAngle(fpPos, targetPosition);

                // 2) Current view angles from MovementContext._rotation
                ulong movementContext = localPlayer.MovementContext;
                Vector2 viewAngles = Memory.ReadValue<Vector2>(
                    movementContext + Offsets.MovementContext._rotation,
                    false
                );

                // 3) Delta and normalization
                Vector2 delta = aimAngle - viewAngles;
                NormalizeAngle(ref delta);

                // 4) Gun angle mapping (clamped to sane limits)
                var gunAngle = new Vector3(
                    DegToRad(delta.X),
                    0.0f,
                    DegToRad(delta.Y)
                );
                gunAngle.X = Math.Clamp(gunAngle.X, -DeviceAimbotConstants.MaxGunAngleRadians, DeviceAimbotConstants.MaxGunAngleRadians);
                gunAngle.Z = Math.Clamp(gunAngle.Z, -DeviceAimbotConstants.MaxGunAngleRadians, DeviceAimbotConstants.MaxGunAngleRadians);

                // 5) Write to _shotDirection
                ulong shotDirectionAddr = localPlayer.PWA + Offsets.ProceduralWeaponAnimation._shotDirection;
                if (!MemDMA.IsValidVirtualAddress(shotDirectionAddr))
                    return;

                Vector3 writeVec = new Vector3(gunAngle.X, DeviceAimbotConstants.ShotDirectionYComponent, gunAngle.Z * -1.0f);
                Memory.WriteValue(shotDirectionAddr, writeVec);

                DebugLogger.LogDebug($"[MemoryAim] Fireport: {fpPos}");
                DebugLogger.LogDebug($"[MemoryAim] Target:   {targetPosition}");
                DebugLogger.LogDebug($"[MemoryAim] AimAngle: {aimAngle}, ViewAngles: {viewAngles}, ={delta}");
                DebugLogger.LogDebug($"[MemoryAim] WriteVec: {writeVec}");
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[MemoryAim] Error: {ex}");
            }
        }

        #region Math Helpers

        private static Vector2 CalcAngle(Vector3 from, Vector3 to)
        {
            Vector3 delta = from - to;
            float length = delta.Length();

            return new Vector2(
                RadToDeg((float)-Math.Atan2(delta.X, -delta.Z)),
                RadToDeg((float)Math.Asin(delta.Y / length))
            );
        }

        private static void NormalizeAngle(ref Vector2 angle)
        {
            NormalizeAngle(ref angle.X);
            NormalizeAngle(ref angle.Y);
        }

        private static void NormalizeAngle(ref float angle)
        {
            while (angle > 180.0f) angle -= 360.0f;
            while (angle < -180.0f) angle += 360.0f;
        }

        private static float DegToRad(float degrees)
            => degrees * ((float)Math.PI / 180.0f);

        private static float RadToDeg(float radians)
            => radians * (180.0f / (float)Math.PI);

        #endregion
    }
}
