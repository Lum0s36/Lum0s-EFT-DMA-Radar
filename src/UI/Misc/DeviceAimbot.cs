/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using System;
using System.Numerics;
using System.Threading;
using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.UI.Misc.Ballistics;
using LoneEftDmaRadar.UI.Misc.DeviceAimbotHelpers;
using SkiaSharp;

namespace LoneEftDmaRadar.UI.Misc
{
    /// <summary>
    /// Device-based hardware aimbot (DeviceAimbot/KMBox) with ballistics prediction.
    /// Orchestrates targeting, aiming, ballistics, and debug subsystems.
    /// </summary>
    public sealed class DeviceAimbot : IDisposable
    {
        #region Fields

        private static DeviceAimbotConfig Config => App.Config.Device;
        private readonly MemDMA _memory;
        private readonly Thread _worker;
        private bool _disposed;

        // Subsystems
        private readonly DeviceAimbotTargeting _targeting = new();
        private readonly DeviceAimbotAiming _aiming = new();
        private readonly DeviceAimbotBallistics _ballistics;
        private readonly DeviceAimbotDebug _debug = new();

        // State
        private string _debugStatus = "Initializing...";
        private AbstractPlayer _lockedTarget;
        private Vector3 _lastFireportPos;
        private bool _hasLastFireport;
        private bool _isEngaged;

        #endregion

        #region Properties

        /// <summary>
        /// Set to true while the aim-key is held (from hotkey/ui).
        /// </summary>
        public bool IsEngaged
        {
            get => _isEngaged;
            set
            {
                if (_isEngaged == value)
                    return;

                _isEngaged = value;

                // Keep MemoryAim in sync with hotkey state (if enabled).
                try
                {
                    if (App.Config.MemWrites.Enabled && App.Config.MemWrites.MemoryAimEnabled)
                        LoneEftDmaRadar.Tarkov.Features.MemWrites.MemoryAim.Instance.SetEngaged(value);
                }
                catch { /* best-effort sync */ }

                if (!value)
                {
                    ResetTarget();
                }
            }
        }

        /// <summary>
        /// Returns the currently locked target (if any). May be null.
        /// </summary>
        public AbstractPlayer LockedTarget => _lockedTarget;

        #endregion

        #region Constructor / Disposal

        public DeviceAimbot(MemDMA memory)
        {
            _memory = memory;
            _ballistics = new DeviceAimbotBallistics(memory);

            // Try auto-connect if configured and device isn't ready.
            if (Config.AutoConnect && !Device.connected)
            {
                try { Device.TryAutoConnect(Config.LastComPort); } catch { /* best-effort */ }
            }
            if (Config.UseKmBoxNet && !DeviceNetController.Connected)
            {
                try
                {
                    DeviceNetController.Connect(Config.KmBoxNetIp, Config.KmBoxNetPort, Config.KmBoxNetMac);
                }
                catch { /* best-effort */ }
            }

            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
                Name = "DeviceAimbotWorker"
            };
            _worker.Start();

            DebugLogger.LogDebug("[DeviceAimbot] Started");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                DebugLogger.LogDebug("[DeviceAimbot] Disposed");
            }
        }

        public void OnRaidEnd()
        {
            ResetTarget();
        }

        #endregion

        #region Main Loop

        private void WorkerLoop()
        {
            _debugStatus = "Worker starting...";

            while (!_disposed)
            {
                try
                {
                    // 1) Check if we're in raid with a valid local player
                    if (!_memory.InRaid)
                    {
                        _debugStatus = "Not in raid";
                        ResetTarget();
                        Thread.Sleep(DeviceAimbotConstants.NotInRaidSleepMs);
                        continue;
                    }

                    if (_memory.LocalPlayer is not LocalPlayer localPlayer)
                    {
                        _debugStatus = "LocalPlayer == null";
                        ResetTarget();
                        Thread.Sleep(DeviceAimbotConstants.NoPlayerSleepMs);
                        continue;
                    }

                    // 2) Check if anything wants to run
                    bool memoryAimActive = App.Config.MemWrites.Enabled && App.Config.MemWrites.MemoryAimEnabled;
                    bool anyAimbotEnabled = Config.Enabled || memoryAimActive;

                    if (!anyAimbotEnabled)
                    {
                        _debugStatus = "Aimbot & MemoryAim disabled";
                        ResetTarget();
                        Thread.Sleep(DeviceAimbotConstants.NotInRaidSleepMs);
                        continue;
                    }

                    if (!memoryAimActive && !Device.connected && !DeviceNetController.Connected)
                    {
                        _debugStatus = "Device/KMBoxNet NOT connected";
                        ResetTarget();
                        Thread.Sleep(DeviceAimbotConstants.DisconnectedSleepMs);
                        continue;
                    }

                    if (_memory.Game is not LocalGameWorld game)
                    {
                        _debugStatus = "Game instance == null";
                        ResetTarget();
                        Thread.Sleep(DeviceAimbotConstants.NoPlayerSleepMs);
                        continue;
                    }

                    // 3) Check engagement
                    if (!IsEngaged)
                    {
                        _debugStatus = "Waiting for aim key";
                        ResetTarget();
                        Thread.Sleep(DeviceAimbotConstants.NotEngagedSleepMs);
                        continue;
                    }

                    // 4) Weapon / Fireport check
                    _debugStatus = "Updating FirearmManager...";
                    localPlayer.UpdateFirearmManager();

                    var fireportPosOpt = localPlayer.FirearmManager?.FireportPosition;
                    bool needsFireport = Config.EnablePrediction || memoryAimActive;

                    if (needsFireport && fireportPosOpt is not Vector3 _)
                    {
                        _debugStatus = "No valid weapon / fireport";
                        ResetTarget();
                        _hasLastFireport = false;
                        Thread.Sleep(DeviceAimbotConstants.NoFireportSleepMs);
                        continue;
                    }

                    if (fireportPosOpt is Vector3 fp)
                    {
                        _lastFireportPos = fp;
                        _hasLastFireport = true;
                    }
                    else
                    {
                        _hasLastFireport = false;
                    }

                    // 5) Target acquisition
                    if (_lockedTarget == null || !_targeting.IsTargetValid(_lockedTarget, localPlayer))
                    {
                        _debugStatus = "Scanning for target...";
                        _lockedTarget = _targeting.FindBestTarget(game, localPlayer);

                        if (_lockedTarget == null)
                        {
                            _debugStatus = "No target in FOV / range";
                            Thread.Sleep(DeviceAimbotConstants.NotEngagedSleepMs);
                            continue;
                        }
                    }

                    _debugStatus = $"Target locked: {_lockedTarget.Name}";

                    // 6) Ballistics
                    var ballisticsInfo = _ballistics.GetBallisticsInfo(localPlayer);
                    if (ballisticsInfo == null || !ballisticsInfo.IsAmmoValid)
                    {
                        _debugStatus = $"Target {_lockedTarget.Name} - No valid ammo (using raw aim)";
                    }
                    else
                    {
                        _debugStatus = $"Target {_lockedTarget.Name} - Ballistics OK";
                    }

                    // 7) Get target bone
                    var selectedBone = memoryAimActive
                        ? App.Config.MemWrites.MemoryAimTargetBone
                        : Config.TargetBone;

                    if (!_targeting.TryGetTargetBone(_lockedTarget, selectedBone, out var boneTransform))
                    {
                        Thread.Sleep(DeviceAimbotConstants.MainLoopSleepMs);
                        continue;
                    }

                    Vector3 targetPos = boneTransform.Position;

                    // 8) Apply ballistics prediction if enabled
                    if (Config.EnablePrediction && fireportPosOpt.HasValue && ballisticsInfo?.IsAmmoValid == true)
                    {
                        targetPos = _ballistics.PredictHitPoint(
                            localPlayer, _lockedTarget, fireportPosOpt.Value, targetPos, ballisticsInfo);
                    }

                    // 9) Aim
                    _aiming.AimAtTarget(localPlayer, _lockedTarget, targetPos, memoryAimActive);

                    Thread.Sleep(DeviceAimbotConstants.MainLoopSleepMs);
                }
                catch (Exception ex)
                {
                    _debugStatus = $"Error: {ex.Message}";
                    DebugLogger.LogDebug($"[DeviceAimbot] Error: {ex}");
                    ResetTarget();
                    Thread.Sleep(DeviceAimbotConstants.ErrorSleepMs);
                }
            }

            _debugStatus = "Worker stopped";
        }

        private void ResetTarget()
        {
            if (_lockedTarget != null)
            {
                _lockedTarget = null;
            }
        }

        #endregion

        #region Debug API

        /// <summary>
        /// Draws debug information on the ESP overlay.
        /// </summary>
        public void DrawDebug(SKCanvas canvas, LocalPlayer localPlayer)
        {
            _debug.DrawDebug(
                canvas,
                localPlayer,
                _memory,
                _debugStatus,
                IsEngaged,
                _lockedTarget,
                _targeting.Stats,
                _ballistics.LastBallistics,
                _hasLastFireport,
                _lastFireportPos);
        }

        /// <summary>
        /// Returns the latest debug snapshot for UI/ESP overlays.
        /// </summary>
        public DeviceAimbotDebugSnapshot GetDebugSnapshot()
        {
            return _debug.GetDebugSnapshot(
                _memory,
                _debugStatus,
                IsEngaged,
                _lockedTarget,
                _targeting.Stats,
                _ballistics.LastBallistics,
                _hasLastFireport,
                _lastFireportPos);
        }

        #endregion
    }

    #region Extension Classes (unchanged)

    public static class Vector3Extensions
    {
        public static Vector3 CalculateDirection(this Vector3 source, Vector3 destination)
        {
            Vector3 dir = destination - source;
            return Vector3.Normalize(dir);
        }
    }

    public static class QuaternionExtensions
    {
        public static Vector3 InverseTransformDirection(this Quaternion rotation, Vector3 direction)
        {
            return Quaternion.Conjugate(rotation).Multiply(direction);
        }

        public static Vector3 Multiply(this Quaternion q, Vector3 v)
        {
            float x = q.X * 2.0f;
            float y = q.Y * 2.0f;
            float z = q.Z * 2.0f;
            float xx = q.X * x;
            float yy = q.Y * y;
            float zz = q.Z * z;
            float xy = q.X * y;
            float xz = q.X * z;
            float yz = q.Y * z;
            float wx = q.W * x;
            float wy = q.W * y;
            float wz = q.W * z;

            Vector3 res;
            res.X = (1.0f - (yy + zz)) * v.X + (xy - wz) * v.Y + (xz + wy) * v.Z;
            res.Y = (xy + wz) * v.X + (1.0f - (xx + zz)) * v.Y + (yz - wx) * v.Z;
            res.Z = (xz - wy) * v.X + (yz + wx) * v.Y + (1.0f - (xx + yy)) * v.Z;

            return res;
        }
    }

    #endregion
}
