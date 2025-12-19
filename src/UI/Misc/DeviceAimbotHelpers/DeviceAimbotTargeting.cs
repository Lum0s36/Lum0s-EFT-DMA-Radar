/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using System;
using System.Collections.Generic;
using System.Numerics;
using LoneEftDmaRadar.Tarkov.GameWorld;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using CameraManagerNew = LoneEftDmaRadar.Tarkov.GameWorld.Camera.CameraManager;

namespace LoneEftDmaRadar.UI.Misc.DeviceAimbotHelpers
{
    /// <summary>
    /// Target candidate for aimbot selection.
    /// </summary>
    public readonly struct TargetCandidate
    {
        public AbstractPlayer Player { get; init; }
        public float FOVDistance { get; init; }
        public float WorldDistance { get; init; }
    }

    /// <summary>
    /// Debug statistics from targeting pass.
    /// </summary>
    public sealed class TargetingStats
    {
        public int TotalPlayers { get; set; }
        public int EligibleType { get; set; }
        public int WithinDistance { get; set; }
        public int HaveSkeleton { get; set; }
        public int W2SPassed { get; set; }
        public int CandidateCount { get; set; }
        public float LastLockedTargetFov { get; set; } = float.NaN;
        public bool LastIsTargetValid { get; set; }

        public void Reset()
        {
            TotalPlayers = 0;
            EligibleType = 0;
            WithinDistance = 0;
            HaveSkeleton = 0;
            W2SPassed = 0;
            CandidateCount = 0;
        }
    }

    /// <summary>
    /// Handles target acquisition and validation for aimbot.
    /// </summary>
    public sealed class DeviceAimbotTargeting
    {
        private static DeviceAimbotConfig Config => App.Config.Device;
        
        public TargetingStats Stats { get; } = new();

        /// <summary>
        /// Finds the best target based on current configuration.
        /// </summary>
        public AbstractPlayer FindBestTarget(LocalGameWorld game, LocalPlayer localPlayer)
        {
            var candidates = new List<TargetCandidate>();
            Stats.Reset();

            float maxDistance = Config.MaxDistance <= 0 ? float.MaxValue : Config.MaxDistance;
            float maxFov = Config.FOV <= 0 ? float.MaxValue : Config.FOV;

            foreach (var player in game.Players)
            {
                Stats.TotalPlayers++;

                if (!ShouldTargetPlayer(player, localPlayer))
                    continue;

                Stats.EligibleType++;

                var distance = Vector3.Distance(localPlayer.Position, player.Position);
                if (distance > maxDistance)
                    continue;

                Stats.WithinDistance++;

                if (player.Skeleton?.BoneTransforms == null)
                    continue;

                Stats.HaveSkeleton++;

                float bestFovForThisPlayer = float.MaxValue;
                bool anyBoneProjected = false;

                foreach (var bone in player.Skeleton.BoneTransforms.Values)
                {
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
                    Stats.W2SPassed++;

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

            Stats.CandidateCount = candidates.Count;

            if (candidates.Count == 0)
                return null;

            return Config.Targeting switch
            {
                DeviceAimbotConfig.TargetingMode.ClosestToCrosshair => SelectMinBy(candidates, x => x.FOVDistance),
                DeviceAimbotConfig.TargetingMode.ClosestDistance => SelectMinBy(candidates, x => x.WorldDistance),
                _ => SelectMinBy(candidates, x => x.FOVDistance)
            };
        }

        /// <summary>
        /// Checks if the current locked target is still valid.
        /// </summary>
        public bool IsTargetValid(AbstractPlayer target, LocalPlayer localPlayer)
        {
            Stats.LastIsTargetValid = false;
            Stats.LastLockedTargetFov = float.NaN;

            if (target == null || !target.IsActive || !target.IsAlive)
                return false;

            float maxDistance = Config.MaxDistance <= 0 ? float.MaxValue : Config.MaxDistance;
            float maxFov = Config.FOV <= 0 ? float.MaxValue : Config.FOV;

            var distance = Vector3.Distance(localPlayer.Position, target.Position);
            if (distance > maxDistance)
                return false;

            if (target.Skeleton?.BoneTransforms == null)
                return false;

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
                Stats.LastLockedTargetFov = float.NaN;
                return false;
            }

            Stats.LastLockedTargetFov = minFov;

            if (minFov < maxFov)
            {
                Stats.LastIsTargetValid = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to get a specific bone transform from the target.
        /// </summary>
        public bool TryGetTargetBone(AbstractPlayer target, Bones targetBone, out UnityTransform boneTransform)
        {
            boneTransform = null;

            if (target?.Skeleton?.BoneTransforms == null)
                return false;

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

        /// <summary>
        /// Determines if a player should be targeted based on config filters.
        /// </summary>
        private bool ShouldTargetPlayer(AbstractPlayer player, LocalPlayer localPlayer)
        {
            bool isDebugPlayer = Stats.TotalPlayers <= DeviceAimbotConstants.DebugPlayerLimit;

            if (isDebugPlayer)
                DebugLogger.LogDebug($"\n[DeviceAimbot] === Checking Player #{Stats.TotalPlayers} ===");

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

        private static AbstractPlayer SelectMinBy(List<TargetCandidate> candidates, Func<TargetCandidate, float> selector)
        {
            if (candidates.Count == 0)
                return null;

            var min = candidates[0];
            float minValue = selector(min);

            for (int i = 1; i < candidates.Count; i++)
            {
                float value = selector(candidates[i]);
                if (value < minValue)
                {
                    minValue = value;
                    min = candidates[i];
                }
            }

            return min.Player;
        }
    }
}
