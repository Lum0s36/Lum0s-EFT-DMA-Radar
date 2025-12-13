/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using LoneEftDmaRadar.Tarkov.Unity.Collections;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Misc.Ballistics;
using SkiaSharp;
using CameraManagerNew = LoneEftDmaRadar.Tarkov.GameWorld.Camera.CameraManager;
using LoneEftDmaRadar.UI.Skia;

namespace LoneEftDmaRadar.UI.Misc
{
    /// <summary>
    /// Device-based hardware aimbot (DeviceAimbot/KMBox) with ballistics prediction.
    /// </summary>
    public sealed class DeviceAimbot : IDisposable
    {
        #region Fields
        // Cached paints/fonts for debug overlay (avoid per-frame allocations)
        private static readonly SKPaint s_bgPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 180),
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
            StrokeWidth = 3
        };
        private static readonly SKFont s_monoFont = SKFonts.InfoWidgetFont; // Consolas 12, subpixel AA

        private static DeviceAimbotConfig Config => App.Config.Device;
        private readonly MemDMA _memory;
        private readonly Thread _worker;
        private bool _disposed;
        private BallisticsInfo _lastBallistics;
        private string _debugStatus = "Initializing...";
        private AbstractPlayer _lockedTarget;

        // FOV / target debug fields
        private int _lastCandidateCount;
        private float _lastLockedTargetFov = float.NaN;
        private bool _lastIsTargetValid;
        private Vector3 _lastFireportPos;
        private bool _hasLastFireport;

        // Per-stage debug counters
        private int _dbgTotalPlayers;
        private int _dbgEligibleType;
        private int _dbgWithinDistance;
        private int _dbgHaveSkeleton;
        private int _dbgW2SPassed;
        private ulong _cachedBreathEffector;
        private ulong _cachedShotEffector;
        private ulong _cachedNewShotRecoil;
        private float _lastRecoilAmount = 1.0f;
        private float _lastSwayAmount = 1.0f;
        
        private readonly List<string> _debugLines = new List<string>(64);
        #endregion

        private void SendDeviceMove(int dx, int dy)
        {
            if (Config.UseKmBoxNet && DeviceNetController.Connected)
            {
                DeviceNetController.Move(dx, dy);
                return;
            }

            Device.move(dx, dy);
        }
		[Flags]
		public enum EProceduralAnimationMask
		{
			Breathing = 1,
			Walking = 2,
			MotionReaction = 4,
			ForceReaction = 8,
			Shooting = 16,
			DrawDown = 32,
			Aiming = 64,
			HandShake = 128,
		}        
        private const int ORIGINAL_PWA_MASK = 
            (int)(EProceduralAnimationMask.MotionReaction |
                  EProceduralAnimationMask.ForceReaction |
                  EProceduralAnimationMask.Shooting |
                  EProceduralAnimationMask.DrawDown |
                  EProceduralAnimationMask.Aiming |
                  EProceduralAnimationMask.Breathing);
        #region Constructor / Disposal

        public DeviceAimbot(MemDMA memory)
        {
            _memory = memory;

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
            _lastRecoilAmount = 1.0f;
            _lastSwayAmount = 1.0f;
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
                        Thread.Sleep(100);
                        continue;
                    }

                    if (_memory.LocalPlayer is not LocalPlayer localPlayer)
                    {
                        _debugStatus = "LocalPlayer == null";
                        ResetTarget();
                        Thread.Sleep(50);
                        continue;
                    }

                    // 3) Check if anything wants to run (hardware aimbot or memory silent aim)
                    bool memoryAimActive = App.Config.MemWrites.Enabled && App.Config.MemWrites.MemoryAimEnabled;
                    bool anyAimbotEnabled = Config.Enabled || memoryAimActive;

                    if (!anyAimbotEnabled)
                    {
                        _debugStatus = "Aimbot & MemoryAim disabled (NoRecoil still active if enabled)";
                        ResetTarget();
                        Thread.Sleep(100);
                        continue;
                    }


                    if (!memoryAimActive && !Device.connected && !DeviceNetController.Connected)
                    {
                        _debugStatus = "Device/KMBoxNet NOT connected (enable MemoryAim to use without device)";
                        ResetTarget();
                        Thread.Sleep(250);
                        continue;
                    }

                    if (_memory.Game is not LocalGameWorld game)
                    {
                        _debugStatus = "Game instance == null";
                        ResetTarget();
                        Thread.Sleep(50);
                        continue;
                    }

                    // 4) Check engagement for AIMBOT only
                    if (!IsEngaged)
                    {
                        _debugStatus = "Waiting for aim key (IsEngaged == false)";
                        ResetTarget();
                        Thread.Sleep(10);
                        continue;
                    }

                    // 5) Weapon / Fireport check
                    _debugStatus = "Updating FirearmManager...";
                    localPlayer.UpdateFirearmManager();

                    var fireportPosOpt = localPlayer.FirearmManager?.FireportPosition;
                    bool needsFireport = Config.EnablePrediction ||
                        (App.Config.MemWrites.Enabled && App.Config.MemWrites.MemoryAimEnabled);

                    if (needsFireport && fireportPosOpt is not Vector3 fireportPos)
                    {
                        _debugStatus = "No valid weapon / fireport (needed for prediction/MemoryAim)";
                        ResetTarget();
                        _hasLastFireport = false;
                        Thread.Sleep(16);
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

                    // 6) Target acquisition
                    if (_lockedTarget == null || !IsTargetValid(_lockedTarget, localPlayer))
                    {
                        _debugStatus = "Scanning for target...";
                        _lockedTarget = FindBestTarget(game, localPlayer);

                        if (_lockedTarget == null)
                        {
                            _debugStatus = "No target in FOV / range";
                            Thread.Sleep(10);
                            continue;
                        }
                    }

                    _debugStatus = $"Target locked: {_lockedTarget.Name}";

                    // 7) Ballistics
                    _lastBallistics = GetBallisticsInfo(localPlayer);
            if (_lastBallistics == null || !_lastBallistics.IsAmmoValid)
            {
                _debugStatus = $"Target {_lockedTarget.Name} - No valid ammo/ballistics (using raw aim)";
            }
            else
            {
                _debugStatus = $"Target {_lockedTarget.Name} - Ballistics OK";
            }

                    // 8) Aim
                    AimAtTarget(localPlayer, _lockedTarget, fireportPosOpt);

                    Thread.Sleep(8); // ~125Hz
                }
                catch (Exception ex)
                {
                    _debugStatus = $"Error: {ex.Message}";
                    DebugLogger.LogDebug($"[DeviceAimbot] Error: {ex}");
                    ResetTarget();
                    Thread.Sleep(100);
                }
            }

            _debugStatus = "Worker stopped";
        }

        #endregion

        #region Targeting

        private AbstractPlayer FindBestTarget(LocalGameWorld game, LocalPlayer localPlayer)
        {
            var candidates = new List<TargetCandidate>();
            _lastCandidateCount = 0;

            // Treat zero/negative limits as "unlimited" so users can clear the field without breaking targeting.
            float maxDistance = Config.MaxDistance <= 0 ? float.MaxValue : Config.MaxDistance;
            float maxFov = Config.FOV <= 0 ? float.MaxValue : Config.FOV;

            // reset per-stage counters
            _dbgTotalPlayers = 0;
            _dbgEligibleType = 0;
            _dbgWithinDistance = 0;
            _dbgHaveSkeleton = 0;
            _dbgW2SPassed = 0;

            foreach (var player in game.Players)
            {
                _dbgTotalPlayers++;

                if (!ShouldTargetPlayer(player, localPlayer))
                    continue;

                _dbgEligibleType++;

                var distance = Vector3.Distance(localPlayer.Position, player.Position);
                if (distance > maxDistance)
                    continue;

                _dbgWithinDistance++;

                // Check if skeleton exists
                if (player.Skeleton?.BoneTransforms == null)
                    continue;

                _dbgHaveSkeleton++;

                // Check if any bone is within FOV
                float bestFovForThisPlayer = float.MaxValue;
                bool anyBoneProjected = false;

                foreach (var bone in player.Skeleton.BoneTransforms.Values)
                {
                    // IMPORTANT: use same W2S style as ESP  "in" + default flags
                    // Disable on-screen check so viewport issues don't discard candidates.
                    if (CameraManagerNew.WorldToScreen(in bone.Position, out var screenPos, false))
                    {
                        anyBoneProjected = true;
                        float fovDist = CameraManagerNew.GetFovMagnitude(screenPos);
                        if (fovDist < bestFovForThisPlayer)
                        {
                            bestFovForThisPlayer = fovDist;
                        }
                    }
                }

                if (anyBoneProjected)
                    _dbgW2SPassed++;

                if (bestFovForThisPlayer <= maxFov)
                {
                    candidates.Add(new TargetCandidate
                    {
                        Player = player,
                        FOVDistance = bestFovForThisPlayer,
                        WorldDistance = distance
                    });
                }
            }

            _lastCandidateCount = candidates.Count;

            if (candidates.Count == 0)
                return null;

            // Select best target based on mode
            return Config.Targeting switch
            {
                DeviceAimbotConfig.TargetingMode.ClosestToCrosshair => candidates.MinBy(x => x.FOVDistance).Player,
                DeviceAimbotConfig.TargetingMode.ClosestDistance => candidates.MinBy(x => x.WorldDistance).Player,
                _ => candidates.MinBy(x => x.FOVDistance).Player
            };
        }

private bool ShouldTargetPlayer(AbstractPlayer player, LocalPlayer localPlayer)
{
    // DEBUG: Log every rejection reason for first few players
    bool isDebugPlayer = _dbgTotalPlayers <= 3;
    
    if (isDebugPlayer)
        DebugLogger.LogDebug($"\n[DeviceAimbot] === Checking Player #{_dbgTotalPlayers} ===");
    
    // Don't target self
    if (player == localPlayer)
    {
        if (isDebugPlayer) DebugLogger.LogDebug($"  ? REJECTED: player == localPlayer");
        return false;
    }
    
    if (player is LocalPlayer)
    {
        if (isDebugPlayer) DebugLogger.LogDebug($"  ? REJECTED: player is LocalPlayer");
        return false;
    }
    
    if (!player.IsActive)
    {
        if (isDebugPlayer) DebugLogger.LogDebug($"  ? REJECTED: !IsActive");
        return false;
    }
    
    if (!player.IsAlive)
    {
        if (isDebugPlayer) DebugLogger.LogDebug($"  ? REJECTED: !IsAlive");
        return false;
    }

    // Check player type filters
    if (player.Type == PlayerType.Teammate)
    {
        if (isDebugPlayer) DebugLogger.LogDebug($"  ? REJECTED: Type is Teammate");
        return false;
    }

    if (isDebugPlayer)
    {
        DebugLogger.LogDebug($"  Player Type: {player.Type}");
        DebugLogger.LogDebug($"  Config Filters:");
        DebugLogger.LogDebug($"    PMC={Config.TargetPMC}, PScav={Config.TargetPlayerScav}");
        DebugLogger.LogDebug($"    AI={Config.TargetAIScav}, Boss={Config.TargetBoss}, Raider={Config.TargetRaider}");
    }

    bool shouldTarget = player.Type switch
    {
        PlayerType.PMC => Config.TargetPMC,
        PlayerType.PScav => Config.TargetPlayerScav,
        PlayerType.AIScav => Config.TargetAIScav,
        PlayerType.AIBoss => Config.TargetBoss,
        PlayerType.AIRaider => Config.TargetRaider,
        PlayerType.Default => Config.TargetAIScav,
        _ => false
    };
    
    if (isDebugPlayer)
    {
        if (shouldTarget)
            DebugLogger.LogDebug($"  ? ACCEPTED!");
        else
            DebugLogger.LogDebug($"  ? REJECTED: Type {player.Type} not in filters or default case");
    }
    
    return shouldTarget;
}
        private bool IsTargetValid(AbstractPlayer target, LocalPlayer localPlayer)
        {
            _lastIsTargetValid = false;
            _lastLockedTargetFov = float.NaN;

            if (target == null || !target.IsActive || !target.IsAlive)
                return false;

            float maxDistance = Config.MaxDistance <= 0 ? float.MaxValue : Config.MaxDistance;
            float maxFov = Config.FOV <= 0 ? float.MaxValue : Config.FOV;

            var distance = Vector3.Distance(localPlayer.Position, target.Position);
            if (distance > maxDistance)
                return false;

            // Check if skeleton exists
            if (target.Skeleton?.BoneTransforms == null)
                return false;

            // Compute min FOV distance for this target
            float minFov = float.MaxValue;
            bool anyBoneProjected = false;

            foreach (var bone in target.Skeleton.BoneTransforms.Values)
            {
                if (CameraManagerNew.WorldToScreen(in bone.Position, out var screenPos, false))
                {
                    anyBoneProjected = true;
                    float fovDist = CameraManagerNew.GetFovMagnitude(screenPos);
                    if (fovDist < minFov)
                        minFov = fovDist;
                }
            }

            if (!anyBoneProjected)
            {
                _lastLockedTargetFov = float.NaN;
                return false;
            }

            _lastLockedTargetFov = minFov;

            if (minFov < maxFov)
            {
                _lastIsTargetValid = true;
                return true;
            }

            return false;
        }

        private void ResetTarget()
        {
            if (_lockedTarget != null)
            {
                _lockedTarget = null;
            }
        }

        private bool TryGetTargetBone(AbstractPlayer target, Bones targetBone, out UnityTransform boneTransform)
        {
            boneTransform = null;

            // Closest-visible bone option
            if (targetBone == Bones.Closest)
            {
                float bestFov = float.MaxValue;
                foreach (var candidate in target.Skeleton.BoneTransforms.Values)
                {
                    if (CameraManagerNew.WorldToScreen(in candidate.Position, out var screenPos))
                    {
                        float fov = CameraManagerNew.GetFovMagnitude(screenPos);
                        if (fov < bestFov)
                        {
                            bestFov = fov;
                            boneTransform = candidate;
                        }
                    }
                }

                if (boneTransform != null)
                    return true;
            }

            // Specific bone
            if (target.Skeleton.BoneTransforms.TryGetValue(targetBone, out boneTransform))
                return true;

            // Fallback to chest if configured bone not found
            return target.Skeleton.BoneTransforms.TryGetValue(Bones.HumanSpine3, out boneTransform);
        }

        #endregion

        #region Aiming

        private void AimAtTarget(LocalPlayer localPlayer, AbstractPlayer target, Vector3? fireportPos)
        {
            // Check if skeleton exists
            if (target.Skeleton?.BoneTransforms == null)
                return;

            var selectedBone = (App.Config.MemWrites.Enabled && App.Config.MemWrites.MemoryAimEnabled)
                ? App.Config.MemWrites.MemoryAimTargetBone
                : Config.TargetBone;

            // Get target bone position
            if (!TryGetTargetBone(target, selectedBone, out var boneTransform))
                return;

            Vector3 targetPos = boneTransform.Position;

            // Apply ballistics prediction if enabled
            if (Config.EnablePrediction && localPlayer.FirearmManager != null && fireportPos.HasValue && _lastBallistics?.IsAmmoValid == true)
            {
                targetPos = PredictHitPoint(localPlayer, target, fireportPos.Value, targetPos);
            }

            // ? Check if MemoryAim is enabled
            if (App.Config.MemWrites.Enabled && App.Config.MemWrites.MemoryAimEnabled)
            {
                ApplyMemoryAim(localPlayer, targetPos);
                DebugLogger.LogDebug($"[DeviceAimbot] Using MemoryAim for target {target.Name}");
                return;
            }

            // Original DeviceAimbot device aiming (only if MemoryAim is disabled)
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

        // 1) Desired angle (same CalcAngle as old cheat)
        Vector2 aimAngle = CalcAngle(fpPos, targetPosition);

        // 2) Current view angles from MovementContext._rotation (NEW OFFSET)
        ulong movementContext = localPlayer.MovementContext;
        Vector2 viewAngles = Memory.ReadValue<Vector2>(
            movementContext + Offsets.MovementContext._rotation, // 0xC4 in your dump
            false
        );

        // 3) Delta and normalization (same as old)
        Vector2 delta = aimAngle - viewAngles;
        NormalizeAngle(ref delta);

        // 4) Gun angle mapping (tighter: no extra damping, clamped to sane limits)
        var gunAngle = new Vector3(
            DegToRad(delta.X),
            0.0f,
            DegToRad(delta.Y)
        );
        const float maxRad = 0.35f; // ~20 degrees
        gunAngle.X = Math.Clamp(gunAngle.X, -maxRad, maxRad);
        gunAngle.Z = Math.Clamp(gunAngle.Z, -maxRad, maxRad);

        // 5) Write to _shotDirection (same as old 0x22C)
        ulong shotDirectionAddr = localPlayer.PWA + Offsets.ProceduralWeaponAnimation._shotDirection;
        if (!MemDMA.IsValidVirtualAddress(shotDirectionAddr))
            return;

        // Keep the exact weird mapping you had before
        Vector3 writeVec = new Vector3(gunAngle.X, -1.0f, gunAngle.Z * -1.0f);
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

        private Vector3 PredictHitPoint(LocalPlayer localPlayer, AbstractPlayer target, Vector3 fireportPos, Vector3 targetPos)
        {
            try
            {
                // Get ballistics info from weapon
                var ballistics = GetBallisticsInfo(localPlayer);
                if (ballistics == null || !ballistics.IsAmmoValid)
                    return targetPos;

                // Get target velocity (only for player scavs and PMCs that have movement)
                Vector3 targetVelocity = Vector3.Zero;
                if (target is ObservedPlayer)
                {
                    try
                    {
                        targetVelocity = _memory.ReadValue<Vector3>(
                            target.MovementContext + Offsets.ObservedMovementController.Velocity,
                            false);
                    }
                    catch
                    {
                        // Velocity read failed, use zero
                    }
                }

                // Run ballistics simulation
                var sim = BallisticsSimulation.Run(ref fireportPos, ref targetPos, ballistics);

                // Apply prediction
                Vector3 predictedPos = targetPos;

                // Add drop compensation
                //predictedPos.Y += sim.DropCompensation;

                // Add lead for moving targets
                if (targetVelocity != Vector3.Zero)
                {
                    float speed = targetVelocity.Length();
                    if (speed > 0.5f) // Only predict if moving faster than 0.5 m/s
                    {
                        Vector3 lead = targetVelocity * sim.TravelTime;
                        predictedPos += lead;
                    }
                }

                return predictedPos;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[DeviceAimbot] Prediction failed: {ex}");
                return targetPos;
            }
        }

        private BallisticsInfo GetBallisticsInfo(LocalPlayer localPlayer)
        {
            try
            {
                var firearmManager = localPlayer.FirearmManager;
                if (firearmManager == null)
                {
                    DebugLogger.LogDebug("[DeviceAimbot] FirearmManager is null");
                    return null;
                }

                var hands = firearmManager.HandsController;
                if (hands.Item2 == false) // Not a weapon
                {
                    DebugLogger.LogDebug("[DeviceAimbot] HandsController is not a weapon");
                    return null;
                }

                ulong itemBase = _memory.ReadPtr(hands.Item1 + Offsets.ItemHandsController.Item, false);
                ulong itemTemplate = _memory.ReadPtr(itemBase + Offsets.LootItem.Template, false);

                // Get ammo template
                var ammoTemplate = FirearmManager.MagazineManager.GetAmmoTemplateFromWeapon(itemBase);
                if (ammoTemplate == 0)
                {
                    DebugLogger.LogDebug("[DeviceAimbot] No ammo template found, using fallback ballistics");
                    return CreateFallbackBallistics();
                }

                // Read ballistics data
                var ballistics = new BallisticsInfo
                {
                    BulletMassGrams = _memory.ReadValue<float>(ammoTemplate + Offsets.AmmoTemplate.BulletMassGram, false),
                    BulletDiameterMillimeters = _memory.ReadValue<float>(ammoTemplate + Offsets.AmmoTemplate.BulletDiameterMilimeters, false),
                    BallisticCoefficient = _memory.ReadValue<float>(ammoTemplate + Offsets.AmmoTemplate.BallisticCoeficient, false)
                };

                // Calculate bullet velocity with mods
                float baseSpeed = _memory.ReadValue<float>(ammoTemplate + Offsets.AmmoTemplate.InitialSpeed, false);
                float velMod = _memory.ReadValue<float>(itemTemplate + Offsets.WeaponTemplate.Velocity, false);

                // Recursively add attachment velocity modifiers
                AddAttachmentVelocity(itemBase, ref velMod);

                ballistics.BulletSpeed = baseSpeed * (1f + (velMod / 100f));

                if (!ballistics.IsAmmoValid)
                {
                    DebugLogger.LogDebug("[DeviceAimbot] Ammo ballistics invalid, using fallback ballistics");
                    return CreateFallbackBallistics();
                }

                return ballistics;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[DeviceAimbot] Failed to get ballistics: {ex}. Using fallback ballistics.");
                return CreateFallbackBallistics();
            }
        }

        private static BallisticsInfo CreateFallbackBallistics()
        {
            // Generic 7.62-ish defaults to keep prediction running when reads fail.
            return new BallisticsInfo
            {
                BulletMassGrams = 8.0f,
                BulletDiameterMillimeters = 7.6f,
                BallisticCoefficient = 0.35f,
                BulletSpeed = 800f
            };
        }

        private void AddAttachmentVelocity(ulong itemBase, ref float velocityModifier)
        {
            try
            {
                var slotsPtr = _memory.ReadPtr(itemBase + Offsets.LootItemMod.Slots, false);
                using var slots = UnityArray<ulong>.Create(slotsPtr, true);

                if (slots.Count > 100) // Sanity check
                    return;

                foreach (var slot in slots.Span)
                {
                    var containedItem = _memory.ReadPtr(slot + Offsets.Slot.ContainedItem, false);
                    if (containedItem == 0)
                        continue;

                    var itemTemplate = _memory.ReadPtr(containedItem + Offsets.LootItem.Template, false);
                    velocityModifier += _memory.ReadValue<float>(itemTemplate + Offsets.ModTemplate.Velocity, false);

                    // Recurse
                    AddAttachmentVelocity(containedItem, ref velocityModifier);
                }
            }
            catch
            {
                // Ignore errors in attachment recursion
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Set to true while the aim-key is held (from hotkey/ui).
        /// </summary>
        private bool _isEngaged;
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
                    // When key is released, drop any lock/target immediately.
                    ResetTarget();
                }
            }
        }

        /// <summary>
        /// Returns the currently locked target (if any). May be null.
        /// </summary>
        public AbstractPlayer LockedTarget => _lockedTarget;

        private readonly struct TargetCandidate
        {
            public AbstractPlayer Player { get; init; }
            public float FOVDistance { get; init; }
            public float WorldDistance { get; init; }
        }

        #region Debug Overlay

        /// <summary>
        /// Draws debug information on the ESP overlay.
        /// </summary>
        public void DrawDebug(SKCanvas canvas, LocalPlayer localPlayer)
        {
            try
            {
                var lines = _debugLines;
                lines.Clear();

                // Header
                lines.Add("=== DeviceAimbot AIMBOT DEBUG ===");
                lines.Add($"Status:       {_debugStatus}");
                lines.Add($"Key State:    {(IsEngaged ? "ENGAGED" : "Idle")}");
                bool devConnected = Device.connected || DeviceNetController.Connected;
                lines.Add($"Device:       {(devConnected ? "Connected" : "Disconnected")}");
                lines.Add($"Enabled:      {(Config.Enabled ? "TRUE" : "FALSE")}");
                lines.Add($"InRaid:       {_memory.InRaid}");
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

                if (_hasLastFireport)
                {
                    lines.Add($"FireportPos:  {_lastFireportPos}");
                }
                else
                {
                    lines.Add("FireportPos:  (no valid fireport)");
                }

                lines.Add("");

                // Per-stage filter stats
                lines.Add("Players (this scan):");
                lines.Add($"  Total:      {_dbgTotalPlayers}");
                lines.Add($"  Type OK:    {_dbgEligibleType}");
                lines.Add($"  InDist:     {_dbgWithinDistance}");
                lines.Add($"  Skeleton:   {_dbgHaveSkeleton}");
                lines.Add($"  W2S OK:     {_dbgW2SPassed}");
                lines.Add($"  Candidates: {_lastCandidateCount}");
                lines.Add("");

                // Target info / FOV diagnostics
                lines.Add($"Config FOV:   {Config.FOV:F1} (pixels from screen center)");
                lines.Add($"TargetValid:  {_lastIsTargetValid}");

                if (_lockedTarget != null && localPlayer != null)
                {
                    var dist = Vector3.Distance(localPlayer.Position, _lockedTarget.Position);
                    lines.Add($"Locked Target: {_lockedTarget.Name} [{_lockedTarget.Type}]");
                    lines.Add($"  Distance:   {dist:F1}m");
                    if (!float.IsNaN(_lastLockedTargetFov) && !float.IsInfinity(_lastLockedTargetFov))
                        lines.Add($"  FOVDist:    {_lastLockedTargetFov:F1}");
                    else
                        lines.Add("  FOVDist:    n/a");

                    lines.Add($"  TargetBone: {Config.TargetBone}");

                    if (_lockedTarget is ObservedPlayer obs)
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
                if (_lastBallistics != null && _lastBallistics.IsAmmoValid)
                {
                    lines.Add("Ballistics:");
                    lines.Add($"  BulletSpeed: {(_lastBallistics.BulletSpeed):F1} m/s");
                    lines.Add($"  Mass:        {_lastBallistics.BulletMassGrams:F2} g");
                    lines.Add($"  BC:          {_lastBallistics.BallisticCoefficient:F3}");
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
                float x = 10;
                float y = 30;
                float lineHeight = 18;

                // Background size
                float maxWidth = 0;
                foreach (var line in lines)
                {
                    // Measure using SKFont (avoids deprecated SKPaint.MeasureText)
                    var width = s_monoFont.MeasureText(line, out _);
                    if (width > maxWidth) maxWidth = width;
                }

                canvas.DrawRect(x - 5, y - 20, maxWidth + 25, lines.Count * lineHeight + 20, s_bgPaint);

                // Text with shadow (fake bold / shading)
                foreach (var line in lines)
                {
                    var paint = line.StartsWith("===") ||
                                line == "Ballistics:" ||
                                line == "Settings:" ||
                                line == "Target Filters:" ||
                                line == "Players (this scan):"
                        ? s_headerPaint
                        : s_textPaint;

                    // New DrawText overload: specify align + SKFont + SKPaint
                    canvas.DrawText(line, x + 1.5f, y + 1.5f, SKTextAlign.Left, s_monoFont, s_shadowPaint);
                    canvas.DrawText(line, x, y, SKTextAlign.Left, s_monoFont, paint);
                    y += lineHeight;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[DeviceAimbot] DrawDebug error: {ex}");
            }
        }

        /// <summary>
        /// Returns the latest debug snapshot for UI/ESP overlays.
        /// </summary>
        public DeviceAimbotDebugSnapshot GetDebugSnapshot()
        {
            try
            {
                var localPlayer = _memory.LocalPlayer;
                float? distanceToTarget = null;
                if (localPlayer != null && _lockedTarget != null)
                    distanceToTarget = Vector3.Distance(localPlayer.Position, _lockedTarget.Position);

                return new DeviceAimbotDebugSnapshot
                {
                    Status = _debugStatus,
                    KeyEngaged = IsEngaged,
                    Enabled = Config.Enabled,
                    DeviceConnected = Device.connected || DeviceNetController.Connected,
                    InRaid = _memory.InRaid,
                    CandidateTotal = _dbgTotalPlayers,
                    CandidateTypeOk = _dbgEligibleType,
                    CandidateInDistance = _dbgWithinDistance,
                    CandidateWithSkeleton = _dbgHaveSkeleton,
                    CandidateW2S = _dbgW2SPassed,
                    CandidateCount = _lastCandidateCount,
                    ConfigFov = Config.FOV,
                    ConfigMaxDistance = Config.MaxDistance,
                    TargetValid = _lastIsTargetValid,
                    LockedTargetName = _lockedTarget?.Name,
                    LockedTargetType = _lockedTarget?.Type,
                    LockedTargetDistance = distanceToTarget,
                    LockedTargetFov = _lastLockedTargetFov,
                    TargetBone = Config.TargetBone,
                    HasFireport = _hasLastFireport,
                    FireportPosition = _hasLastFireport ? _lastFireportPos : (Vector3?)null,
                    BallisticsValid = _lastBallistics?.IsAmmoValid ?? false,
                    BulletSpeed = _lastBallistics?.BulletSpeed,
                    PredictionEnabled = Config.EnablePrediction,
                    TargetingMode = Config.Targeting
                };
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[DeviceAimbot] GetDebugSnapshot error: {ex}");
                return null;
            }
        }
        #endregion
        #endregion
        
    }

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
}
