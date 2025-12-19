/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Misc;
using LoneEftDmaRadar.Tarkov.Unity.Collections;
using LoneEftDmaRadar.UI.Misc;
using System.Runtime.InteropServices;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Camera
{
    /// <summary>
    /// Sight component structures for optic/scope detection.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    internal readonly struct SightComponent
    {
        [FieldOffset((int)Offsets.SightComponent._template)]
        private readonly ulong pSightInterface;

        [FieldOffset((int)Offsets.SightComponent.ScopesSelectedModes)]
        private readonly ulong pScopeSelectedModes;

        [FieldOffset((int)Offsets.SightComponent.SelectedScope)]
        private readonly int SelectedScope;

        [FieldOffset((int)Offsets.SightComponent.ScopeZoomValue)]
        public readonly float ScopeZoomValue;

        public readonly float GetZoomLevel()
        {
            try
            {
                using var zoomArray = SightInterface.Zooms;
                if (SelectedScope >= zoomArray.Count || SelectedScope < 0 || SelectedScope > CameraConstants.MaxSelectedScopeIndex)
                    return -1.0f;

                using var selectedScopeModes = UnityArray<int>.Create(pScopeSelectedModes, false);
                int selectedScopeMode = SelectedScope >= selectedScopeModes.Count ? 0 : selectedScopeModes[SelectedScope];
                ulong zoomAddr = zoomArray[SelectedScope] + UnityArray<float>.ArrBaseOffset + (uint)selectedScopeMode * CameraConstants.ZoomElementSize;

                float zoomLevel = Memory.ReadValue<float>(zoomAddr, false);
                if (zoomLevel.IsNormalOrZero() && zoomLevel >= 0f && zoomLevel < CameraConstants.MaxValidZoom)
                    return zoomLevel;

                return -1.0f;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"ERROR in GetZoomLevel: {ex}");
                return -1.0f;
            }
        }

        public readonly SightInterface SightInterface => Memory.ReadValue<SightInterface>(pSightInterface);
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    internal readonly struct SightInterface
    {
        [FieldOffset((int)Offsets.SightInterface.Zooms)]
        private readonly ulong pZooms;

        public readonly UnityArray<ulong> Zooms => UnityArray<ulong>.Create(pZooms, true);
    }
}
