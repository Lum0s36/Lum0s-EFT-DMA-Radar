/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using System;
using System.Numerics;
using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.Unity.Collections;
using LoneEftDmaRadar.UI.Misc.Ballistics;
using CameraManagerNew = LoneEftDmaRadar.Tarkov.GameWorld.Camera.CameraManager;

namespace LoneEftDmaRadar.UI.Misc.DeviceAimbotHelpers
{
    /// <summary>
    /// Handles ballistics calculation and prediction for aimbot.
    /// </summary>
    public sealed class DeviceAimbotBallistics
    {
        private readonly MemDMA _memory;

        public DeviceAimbotBallistics(MemDMA memory)
        {
            _memory = memory;
        }

        /// <summary>
        /// Last calculated ballistics info.
        /// </summary>
        public BallisticsInfo LastBallistics { get; private set; }

        /// <summary>
        /// Gets ballistics info from the local player's current weapon.
        /// </summary>
        public BallisticsInfo GetBallisticsInfo(LocalPlayer localPlayer)
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

                LastBallistics = ballistics;
                return ballistics;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[DeviceAimbot] Failed to get ballistics: {ex}. Using fallback ballistics.");
                return CreateFallbackBallistics();
            }
        }

        /// <summary>
        /// Predicts the hit point for a moving target with ballistics simulation.
        /// </summary>
        public Vector3 PredictHitPoint(
            LocalPlayer localPlayer,
            AbstractPlayer target,
            Vector3 fireportPos,
            Vector3 targetPos,
            BallisticsInfo ballistics)
        {
            try
            {
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

                // Add lead for moving targets
                if (targetVelocity != Vector3.Zero)
                {
                    float speed = targetVelocity.Length();
                    if (speed > DeviceAimbotConstants.MinVelocityForPrediction)
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

        private void AddAttachmentVelocity(ulong itemBase, ref float velocityModifier)
        {
            try
            {
                var slotsPtr = _memory.ReadPtr(itemBase + Offsets.LootItemMod.Slots, false);
                using var slots = UnityArray<ulong>.Create(slotsPtr, true);

                if (slots.Count > DeviceAimbotConstants.MaxSlotsCount)
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

        private static BallisticsInfo CreateFallbackBallistics()
        {
            return new BallisticsInfo
            {
                BulletMassGrams = DeviceAimbotConstants.FallbackBulletMassGrams,
                BulletDiameterMillimeters = DeviceAimbotConstants.FallbackBulletDiameterMm,
                BallisticCoefficient = DeviceAimbotConstants.FallbackBallisticCoefficient,
                BulletSpeed = DeviceAimbotConstants.FallbackBulletSpeed
            };
        }
    }
}
