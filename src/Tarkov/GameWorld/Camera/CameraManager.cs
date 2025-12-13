/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Misc;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.Unity.Collections;
using LoneEftDmaRadar.Tarkov.Unity;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Misc;
using SkiaSharp;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using VmmSharpEx;
using VmmSharpEx.Scatter;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Camera
{
    public sealed class CameraManager
    {
        private const int VIEWPORT_TOLERANCE = 800;

        static CameraManager()
        {
            MemDMA.ProcessStarting += MemDMA_ProcessStarting;
            MemDMA.ProcessStopped += MemDMA_ProcessStopped;
        }

        private static void MemDMA_ProcessStarting(object sender, EventArgs e) { }
        private static void MemDMA_ProcessStopped(object sender, EventArgs e) { }
        
        // Compatibility stub for existing code (PR #6 doesn't track this, always return 1.0)
        public static float ScopeMagnification => 1.0f;
        
        public static ulong FPSCameraPtr { get; private set; }
        public static ulong OpticCameraPtr { get; private set; }
        public static ulong ActiveCameraPtr { get; private set; }

        private static readonly Lock _viewportSync = new();
        public static Rectangle Viewport { get; private set; }
        public static SKPoint ViewportCenter => new SKPoint(Viewport.Width / 2f, Viewport.Height / 2f);
        public static bool IsScoped { get; private set; }
        public static bool IsADS { get; private set; }
        public static bool IsInitialized { get; private set; } = false;
        private static float _fov;
        private static float _aspect;
        private static readonly ViewMatrix _viewMatrix = new();
        public static Vector3 CameraPosition => new(_viewMatrix.M14, _viewMatrix.M24, _viewMatrix.Translation.Z);
    
        // Optic camera cache for performance (from commit 7e83931)
        private static readonly List<ulong> _potentialOpticCameras = new();
        private static bool _hasSearchedForPotentialCameras = false;
        private static bool _useFpsCameraForCurrentAds = false;

        public static void Reset()
        {
            var identity = Matrix4x4.Identity;
            _viewMatrix.Update(ref identity);
            ActiveCameraPtr = 0;
            OpticCameraPtr = 0;
            _fov = 0f;
            _aspect = 0f;
            IsInitialized = false;
            _potentialOpticCameras.Clear();
            _hasSearchedForPotentialCameras = false;
            _useFpsCameraForCurrentAds = false;
            
            // Don't reset viewport to empty - re-initialize with valid resolution
            UpdateViewportRes();
        }

        public ulong FPSCamera { get; }
        public ulong OpticCamera { get; }
        private ulong _fpsMatrixAddress;
        private ulong _opticMatrixAddress;
        private bool OpticCameraActive => OpticCameraPtr != 0;

        public static void UpdateViewportRes()
        {
            lock (_viewportSync)
            {
                int width, height;

                if (App.Config.UI.EspScreenWidth > 0 && App.Config.UI.EspScreenHeight > 0)
                {
                    width = App.Config.UI.EspScreenWidth;
                    height = App.Config.UI.EspScreenHeight;
                }
                else
                {
                    var targetMonitor = MonitorInfo.GetMonitor(App.Config.UI.EspTargetScreen);
                    if (targetMonitor != null)
                    {
                        width = targetMonitor.Width;
                        height = targetMonitor.Height;
                        DebugLogger.LogDebug($"[CameraManager] Using target monitor {targetMonitor.Index} resolution: {width}x{height}");
                    }
                    else
                    {
                        width = (int)App.Config.UI.Resolution.Width;
                        height = (int)App.Config.UI.Resolution.Height;
                        DebugLogger.LogDebug($"[CameraManager] Falling back to game resolution: {width}x{height}");
                    }
                }

                if (width <= 0 || height <= 0)
                {
                    width = 1920;
                    height = 1080;
                    DebugLogger.LogDebug($"[CameraManager] Invalid resolution, using fallback: {width}x{height}");
                }

                Viewport = new Rectangle(0, 0, width, height);
                DebugLogger.LogDebug($"[CameraManager] Viewport updated to {width}x{height}");
            }
        }

        public static bool WorldToScreen(ref readonly Vector3 worldPos, out SKPoint scrPos, bool onScreenCheck = false, bool useTolerance = false) 
        {
            try
            {
                float w = Vector3.Dot(_viewMatrix.Translation, worldPos) + _viewMatrix.M44;

                if (w < 0.098f)
                {
                    scrPos = default;
                    return false;
                }

                float x = Vector3.Dot(_viewMatrix.Right, worldPos) + _viewMatrix.M14;
                float y = Vector3.Dot(_viewMatrix.Up, worldPos) + _viewMatrix.M24;

                if (IsScoped)
                {
                    float angleRadHalf = (MathF.PI / 180f) * _fov * 0.5f;
                    float angleCtg = MathF.Cos(angleRadHalf) / MathF.Sin(angleRadHalf);

                    x /= angleCtg * _aspect * 0.5f;
                    y /= angleCtg * 0.5f;
                }

                var center = ViewportCenter;
                scrPos = new()
                {
                    X = center.X * (1f + x / w),
                    Y = center.Y * (1f - y / w)
                };

                if (onScreenCheck)
                {
                    int left = useTolerance ? Viewport.Left - VIEWPORT_TOLERANCE : Viewport.Left;
                    int right = useTolerance ? Viewport.Right + VIEWPORT_TOLERANCE : Viewport.Right;
                    int top = useTolerance ? Viewport.Top - VIEWPORT_TOLERANCE : Viewport.Top;
                    int bottom = useTolerance ? Viewport.Bottom + VIEWPORT_TOLERANCE : Viewport.Bottom;

                    if (scrPos.X < left || scrPos.X > right || scrPos.Y < top || scrPos.Y > bottom)
                    {
                        scrPos = default;
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"ERROR in WorldToScreen: {ex}");
                scrPos = default;
                return false;
            }
        }

        public CameraManager()
        {
            if (IsInitialized)
                return;

            _potentialOpticCameras.Clear();
            _hasSearchedForPotentialCameras = false;
            _useFpsCameraForCurrentAds = false;

            try
            {
                DebugLogger.LogDebug("=== CameraManager Initialization ===");
                
                // Use signature-based lookup with fallback (from commit 95ec910)
                var allCamerasPtr = AllCameras.GetPtr(Memory.UnityBase);
                
                if (allCamerasPtr == 0)
                {
                    DebugLogger.LogDebug(" CRITICAL: AllCameras pointer is NULL!");
                    DebugLogger.LogDebug("This means the AllCameras offset is likely wrong.");
                    throw new InvalidOperationException("AllCameras pointer is NULL - offset may be outdated");
                }

                if (allCamerasPtr > 0x7FFFFFFFFFFF)
                {
                    DebugLogger.LogDebug($" CRITICAL: AllCameras pointer is invalid: 0x{allCamerasPtr:X}");
                    throw new InvalidOperationException($"Invalid AllCameras pointer: 0x{allCamerasPtr:X}");
                }

                // AllCameras is a List<Camera*>
                // Structure: +0x0 = items array pointer, +0x8 = count (int)
                var listItemsPtr = Memory.ReadPtr(allCamerasPtr + 0x0, false);
                var count = Memory.ReadValue<int>(allCamerasPtr + 0x8, false);

                DebugLogger.LogDebug($"\nList Structure:");
                DebugLogger.LogDebug($"  Items Pointer: 0x{listItemsPtr:X}");
                DebugLogger.LogDebug($"  Count: {count}");

                if (listItemsPtr == 0)
                {
                    DebugLogger.LogDebug(" CRITICAL: List items pointer is NULL!");
                    throw new InvalidOperationException("Camera list items pointer is NULL");
                }

                if (count <= 0)
                {
                    DebugLogger.LogDebug(" CRITICAL: Camera count is 0 or negative!");
                    DebugLogger.LogDebug("This usually means you're not in a raid yet.");
                    throw new InvalidOperationException($"No cameras in list (count: {count})");
                }

                if (count > 100)
                {
                    DebugLogger.LogDebug($" WARNING: Camera count seems high: {count}");
                    DebugLogger.LogDebug("This might indicate memory corruption or wrong structure.");
                }

                // Only search for FPS camera during initialization (from commit 7e83931)
                var fps = FindFpsCamera(listItemsPtr, count);

                if (fps == 0)
                {
                    DebugLogger.LogDebug("\n CRITICAL: Could not find required FPS Camera!");
                    throw new InvalidOperationException("Could not find required FPS Camera!");
                }

                FPSCamera = fps;

                DebugLogger.LogDebug("\n=== Getting Matrix Addresses ===");
                _fpsMatrixAddress = GetMatrixAddress(FPSCamera, "FPS");

                FPSCameraPtr = FPSCamera;
                OpticCameraPtr = 0;
                ActiveCameraPtr = 0;
                _opticMatrixAddress = 0;

                DebugLogger.LogDebug($"  FPS Camera: 0x{FPSCamera:X}");
                DebugLogger.LogDebug($"  GameObject: 0x{Memory.ReadPtr(FPSCamera + UnitySDK.UnityOffsets.Component_GameObjectOffset, false):X}");
                DebugLogger.LogDebug($"  Matrix Address: 0x{_fpsMatrixAddress:X}");

                VerifyViewMatrix(_fpsMatrixAddress, "FPS");

                _hasSearchedForPotentialCameras = false;

                IsInitialized = true;
                DebugLogger.LogDebug("=== CameraManager Initialization Complete ===\n");
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($" CameraManager initialization failed: {ex}");
                throw;
            }
        }

        private static ulong GetMatrixAddress(ulong cameraPtr, string cameraType)
        {
            var gameObject = Memory.ReadPtr(cameraPtr + UnitySDK.UnityOffsets.Component_GameObjectOffset, false);

            if (gameObject == 0 || gameObject > 0x7FFFFFFFFFFF)
                throw new InvalidOperationException($"Invalid {cameraType} GameObject: 0x{gameObject:X}");

            var ptr1 = Memory.ReadPtr(gameObject + UnitySDK.UnityOffsets.GameObject_ComponentsOffset, false);

            if (ptr1 == 0 || ptr1 > 0x7FFFFFFFFFFF)
                throw new InvalidOperationException($"Invalid {cameraType} Ptr1 (GameObject+UnitySDK.UnityOffsets.GameObject_ComponentsOffset): 0x{ptr1:X}");
                
            var matrixAddress = Memory.ReadPtr(ptr1 + 0x18, false);

            if (matrixAddress == 0 || matrixAddress > 0x7FFFFFFFFFFF)
                throw new InvalidOperationException($"Invalid {cameraType} matrixAddress (Ptr1+0x18): 0x{matrixAddress:X}");

            return matrixAddress;
        }

        private static void VerifyViewMatrix(ulong matrixAddress, string name)
        {
            try
            {
                DebugLogger.LogDebug($"\n{name} Matrix @ 0x{matrixAddress:X}:");
                var vm = Memory.ReadValue<Matrix4x4>(matrixAddress + UnitySDK.UnityOffsets.Camera_ViewMatrixOffset, false);

                float rightMag = MathF.Sqrt(vm.M11 * vm.M11 + vm.M12 * vm.M12 + vm.M13 * vm.M13);
                float upMag = MathF.Sqrt(vm.M21 * vm.M21 + vm.M22 * vm.M22 + vm.M23 * vm.M23);
                float fwdMag = MathF.Sqrt(vm.M31 * vm.M31 + vm.M32 * vm.M32 + vm.M33 * vm.M33);

                DebugLogger.LogDebug($"  M44: {vm.M44:F6}");
                DebugLogger.LogDebug($"  Translation: ({vm.M41:F2}, {vm.M42:F2}, {vm.M43:F2})");
                DebugLogger.LogDebug($"  Right mag: {rightMag:F4}, Up mag: {upMag:F4}, Fwd mag: {fwdMag:F4}");
                DebugLogger.LogDebug($"  Right: ({vm.M11:F3}, {vm.M12:F3}, {vm.M13:F3})");
                DebugLogger.LogDebug($"  Up: ({vm.M21:F3}, {vm.M22:F3}, {vm.M23:F3})");
                DebugLogger.LogDebug($"  Forward: ({vm.M31:F3}, {vm.M32:F3}, {vm.M33:F3})");
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"ERROR verifying ViewMatrix for {name}: {ex}");
            }
        }

        /// <summary>
        /// Finds only the FPS camera during initialization (from commit 7e83931)
        /// </summary>
        private static ulong FindFpsCamera(ulong listItemsPtr, int count)
        {
            ulong fpsCamera = 0;

            DebugLogger.LogDebug($"\n=== Searching for FPS Camera ===");
            DebugLogger.LogDebug($"List Items Ptr: 0x{listItemsPtr:X}");
            DebugLogger.LogDebug($"Camera Count: {count}");

            for (int i = 0; i < Math.Min(count, 100); i++)
            {
                try
                {
                    ulong cameraEntryAddr = listItemsPtr + (uint)(i * 0x8);
                    var cameraPtr = Memory.ReadPtr(cameraEntryAddr, false);

                    if (cameraPtr == 0 || cameraPtr > 0x7FFFFFFFFFFF)
                        continue;

                    var gameObjectPtr = Memory.ReadPtr(cameraPtr + UnitySDK.UnityOffsets.Component_GameObjectOffset, false);
                    if (gameObjectPtr == 0 || gameObjectPtr > 0x7FFFFFFFFFFF)
                        continue;

                    var namePtr = Memory.ReadPtr(gameObjectPtr + UnitySDK.UnityOffsets.GameObject_NameOffset, false);
                    if (namePtr == 0 || namePtr > 0x7FFFFFFFFFFF)
                        continue;

                    var name = Memory.ReadUtf8String(namePtr, 64, false);
                    if (string.IsNullOrEmpty(name) || name.Length < 3)
                        continue;

                    bool isFPS = name.Contains("FPS", StringComparison.OrdinalIgnoreCase) &&
                                name.Contains("Camera", StringComparison.OrdinalIgnoreCase);

                    if (isFPS)
                    {
                        fpsCamera = cameraPtr;
                        DebugLogger.LogDebug($"       Found FPS Camera");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.LogDebug($"  [{i:D2}] Exception: {ex.Message}");
                }
            }

            DebugLogger.LogDebug($"\n=== Search Results ===");
            DebugLogger.LogDebug($"  FPS Camera:   {(fpsCamera != 0 ? $" Found @ 0x{fpsCamera:X}" : " NOT FOUND")}");

            return fpsCamera;
        }

        /// <summary>
        /// Validates an Optic Camera by checking if its view matrix contains valid data
        /// </summary>
        private static bool ValidateOpticCameraMatrix(ulong cameraPtr)
        {
            try
            {
                var matrixAddress = GetMatrixAddress(cameraPtr, "Optic");

                var vm = Memory.ReadValue<Matrix4x4>(matrixAddress + UnitySDK.UnityOffsets.Camera_ViewMatrixOffset, false);

                if (Math.Abs(vm.M44) < 0.001f)
                {
                    DebugLogger.LogDebug($"       Optic Camera validation failed: M44 is zero ({vm.M44})");
                    return false;
                }

                if (Math.Abs(vm.M41) < 0.001f && Math.Abs(vm.M42) < 0.001f && Math.Abs(vm.M43) < 0.001f)
                {
                    DebugLogger.LogDebug($"       Optic Camera validation failed: Translation is all zeros");
                    return false;
                }

                float rightMag = MathF.Sqrt(vm.M11 * vm.M11 + vm.M12 * vm.M12 + vm.M13 * vm.M13);
                float upMag = MathF.Sqrt(vm.M21 * vm.M21 + vm.M22 * vm.M22 + vm.M23 * vm.M23);
                float fwdMag = MathF.Sqrt(vm.M31 * vm.M31 + vm.M32 * vm.M32 + vm.M33 * vm.M33);

                if (rightMag < 0.1f && upMag < 0.1f && fwdMag < 0.1f)
                {
                    DebugLogger.LogDebug($"       Optic Camera validation failed: All rotation magnitudes too small (R:{rightMag:F3} U:{upMag:F3} F:{fwdMag:F3})");
                    return false;
                }

                const float minMagnitude = 0.1f;
                const float maxMagnitude = 100.0f;

                bool hasValidVectors = false;
                if (rightMag >= minMagnitude && rightMag <= maxMagnitude)
                    hasValidVectors = true;
                if (upMag >= minMagnitude && upMag <= maxMagnitude)
                    hasValidVectors = true;
                if (fwdMag >= minMagnitude && fwdMag <= maxMagnitude)
                    hasValidVectors = true;

                if (!hasValidVectors)
                {
                    DebugLogger.LogDebug($"       Optic Camera validation failed: All vector magnitudes out of reasonable range (R:{rightMag:F3} U:{upMag:F3} F:{fwdMag:F3})");
                    return false;
                }

                DebugLogger.LogDebug($"       Optic Camera validation passed: M44:{vm.M44:F3} Trans:({vm.M41:F2},{vm.M42:F2},{vm.M43:F2}) Mags:(R:{rightMag:F3} U:{upMag:F3} F:{fwdMag:F3})");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"       Optic Camera validation exception: {ex.Message}");
                return false;
            }
        }

        private bool CheckIfScoped(LocalPlayer localPlayer)
        {
            try
            {
                if (localPlayer is null)
                    return false;

                if (!OpticCameraActive)
                    return false;

                var opticsPtr = Memory.ReadPtr(localPlayer.PWA + Offsets.ProceduralWeaponAnimation._optics);

                using var optics = UnityList<VmmPointer>.Create(opticsPtr, true);

                if (optics.Count > 0)
                {
                    var pSightComponent = Memory.ReadPtr(optics[0] + Offsets.SightNBone.Mod);
                    var sightComponent = Memory.ReadValue<SightComponent>(pSightComponent);

                    if (sightComponent.ScopeZoomValue != 0f)
                    {
                        bool result = sightComponent.ScopeZoomValue > 1f;
                        return result;
                    }

                    float zoomLevel = sightComponent.GetZoomLevel();
                    bool zoomResult = zoomLevel > 1f;
                    return zoomResult;
                }

                return false;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"CheckIfScoped() ERROR: {ex}");
                return false;
            }
        }

        public void OnRealtimeLoop(VmmScatter scatter, LocalPlayer localPlayer)
        {
            try
            {
                IsADS = localPlayer?.CheckIfADS() ?? false;

                // Reset flag when not ADS (from commit 7e83931)
                if (!IsADS)
                {
                    _useFpsCameraForCurrentAds = false;
                }

                // Cached optic camera search logic (from commit 7e83931)
                if (IsADS && OpticCameraPtr == 0)
                {
                    if (!_useFpsCameraForCurrentAds)
                    {
                        if (_hasSearchedForPotentialCameras && _potentialOpticCameras.Count > 0)
                        {
                            if (ValidateOpticCameras())
                            {
                                DebugLogger.LogDebug("Using validated optic camera from cache");
                            }
                            else
                            {
                                _useFpsCameraForCurrentAds = true;
                                DebugLogger.LogDebug("No valid optic cameras in cache, using FPS camera for this ADS");
                            }
                        }
                        else
                        {
                            SearchOpticCameras();
                            if (_potentialOpticCameras.Count > 0)
                            {
                                if (ValidateOpticCameras())
                                {
                                    DebugLogger.LogDebug("Using validated optic camera from cache");
                                }
                                else
                                {
                                    _useFpsCameraForCurrentAds = true;
                                    DebugLogger.LogDebug("No valid optic cameras found, using FPS camera for this ADS");
                                }
                            }
                            else
                            {
                                _useFpsCameraForCurrentAds = true;
                                DebugLogger.LogDebug("No potential optic cameras found, using FPS camera");
                            }
                        }
                    }
                }

                IsScoped = IsADS && CheckIfScoped(localPlayer);

                ulong vmAddr;
                if (IsADS && IsScoped && OpticCameraPtr != 0 && _opticMatrixAddress != 0)
                {
                    vmAddr = _opticMatrixAddress + UnitySDK.UnityOffsets.Camera_ViewMatrixOffset;
                }
                else
                {
                    vmAddr = _fpsMatrixAddress + UnitySDK.UnityOffsets.Camera_ViewMatrixOffset;
                    if (OpticCameraPtr == 0 || _opticMatrixAddress == 0)
                        IsScoped = false;
                }

                scatter.PrepareReadValue<Matrix4x4>(vmAddr);
                scatter.Completed += (sender, s) =>
                {
                    if (s.ReadValue<Matrix4x4>(vmAddr, out var vm))
                    {
                        if (!Unsafe.IsNullRef(ref vm))
                            _viewMatrix.Update(ref vm);
                    }
                };

                if (IsScoped)
                {
                    var fovAddr = FPSCamera + UnitySDK.UnityOffsets.Camera_FOVOffset;
                    var aspectAddr = FPSCamera + UnitySDK.UnityOffsets.Camera_AspectRatioOffset;

                    scatter.PrepareReadValue<float>(fovAddr);
                    scatter.PrepareReadValue<float>(aspectAddr);

                    scatter.Completed += (sender, s) =>
                    {
                        if (s.ReadValue<float>(fovAddr, out var fov))
                            _fov = fov;

                        if (s.ReadValue<float>(aspectAddr, out var aspect))
                            _aspect = aspect;
                    };
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"ERROR in CameraManager OnRealtimeLoop: {ex}");
            }
        }

        /// <summary>
        /// Validates cached potential optic cameras and sets up a valid one if found (from commit 7e83931)
        /// </summary>
        private bool ValidateOpticCameras()
        {
            try
            {
                DebugLogger.LogDebug($"Validating {_potentialOpticCameras.Count} cached optic cameras...");

                foreach (var cameraPtr in _potentialOpticCameras)
                {
                    if (ValidateOpticCameraMatrix(cameraPtr))
                    {
                        // Found a valid optic camera, set it up
                        OpticCameraPtr = cameraPtr;
                        _opticMatrixAddress = GetMatrixAddress(cameraPtr, "Optic");
                        DebugLogger.LogDebug($"Successfully validated and set up Optic Camera: 0x{cameraPtr:X}");
                        VerifyViewMatrix(_opticMatrixAddress, "Optic");
                        return true;
                    }
                }

                DebugLogger.LogDebug("No valid optic camera found in cache");
                return false;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"ValidateOpticCameras error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Search and cache all potential optic cameras (from commit 7e83931)
        /// </summary>
        private void SearchOpticCameras()
        {
            try
            {
                if (_hasSearchedForPotentialCameras) return;

                var allCamerasPtr = AllCameras.GetPtr(Memory.UnityBase);
                if (allCamerasPtr == 0) return;

                var listItemsPtr = Memory.ReadPtr(allCamerasPtr + 0x0, false);
                var count = Memory.ReadValue<int>(allCamerasPtr + 0x8, false);

                DebugLogger.LogDebug($"Searching for potential optic cameras... ({count} cameras available)");

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        ulong cameraEntryAddr = listItemsPtr + (uint)(i * 0x8);
                        var cameraPtr = Memory.ReadPtr(cameraEntryAddr, false);

                        if (cameraPtr == 0 || cameraPtr > 0x7FFFFFFFFFFF) continue;

                        var gameObjectPtr = Memory.ReadPtr(cameraPtr + UnitySDK.UnityOffsets.Component_GameObjectOffset, false);
                        if (gameObjectPtr == 0 || gameObjectPtr > 0x7FFFFFFFFFFF) continue;

                        var namePtr = Memory.ReadPtr(gameObjectPtr + UnitySDK.UnityOffsets.GameObject_NameOffset, false);
                        if (namePtr == 0 || namePtr > 0x7FFFFFFFFFFF) continue;

                        var name = Memory.ReadUtf8String(namePtr, 64, false);
                        if (string.IsNullOrEmpty(name) || name.Length < 3) continue;

                        // Check for potential Optic Camera (Clone or Optic)
                        bool isPotentialOptic = (name.Contains("Clone", StringComparison.OrdinalIgnoreCase) ||
                                                 name.Contains("Optic", StringComparison.OrdinalIgnoreCase)) &&
                                                 name.Contains("Camera", StringComparison.OrdinalIgnoreCase);

                        if (isPotentialOptic)
                        {
                            DebugLogger.LogDebug($"Found potential Optic Camera: {name}");
                            _potentialOpticCameras.Add(cameraPtr);
                        }
                    }
                    catch
                    {
                        // continue searching
                    }
                }

                _hasSearchedForPotentialCameras = true;
                DebugLogger.LogDebug($"Potential optic camera search complete: {_potentialOpticCameras.Count} cameras cached");
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"SearchOpticCameras error: {ex.Message}");
            }
        }

        #region SightComponent Structures
        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        private readonly struct SightComponent
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
                    if (SelectedScope >= zoomArray.Count || SelectedScope is < 0 or > 10)
                        return -1.0f;

                    using var selectedScopeModes = UnityArray<int>.Create(pScopeSelectedModes, false);
                    int selectedScopeMode = SelectedScope >= selectedScopeModes.Count ? 0 : selectedScopeModes[SelectedScope];
                    ulong zoomAddr = zoomArray[SelectedScope] + UnityArray<float>.ArrBaseOffset + (uint)selectedScopeMode * 0x4;

                    float zoomLevel = Memory.ReadValue<float>(zoomAddr, false);
                    if (zoomLevel.IsNormalOrZero() && zoomLevel is >= 0f and < 100f)
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
        private readonly struct SightInterface
        {
            [FieldOffset((int)Offsets.SightInterface.Zooms)]
            private readonly ulong pZooms;

            public readonly UnityArray<ulong> Zooms => UnityArray<ulong>.Create(pZooms, true);
        }
        #endregion

        public static CameraDebugSnapshot GetDebugSnapshot()
        {
            return new CameraDebugSnapshot
            {
                IsADS = IsADS,
                IsScoped = IsScoped,
                FPSCamera = FPSCameraPtr,
                OpticCamera = OpticCameraPtr,
                ActiveCamera = ActiveCameraPtr,
                Fov = _fov,
                Aspect = _aspect,
                M14 = _viewMatrix.M14,
                M24 = _viewMatrix.M24,
                M44 = _viewMatrix.M44,
                RightX = _viewMatrix.Right.X,
                RightY = _viewMatrix.Right.Y,
                RightZ = _viewMatrix.Right.Z,
                UpX = _viewMatrix.Up.X,
                UpY = _viewMatrix.Up.Y,
                UpZ = _viewMatrix.Up.Z,
                TransX = _viewMatrix.Translation.X,
                TransY = _viewMatrix.Translation.Y,
                TransZ = _viewMatrix.Translation.Z,
                ViewportW = Viewport.Width,
                ViewportH = Viewport.Height
            };
        }

        public readonly struct CameraDebugSnapshot
        {
            public bool IsADS { get; init; }
            public bool IsScoped { get; init; }
            public ulong FPSCamera { get; init; }
            public ulong OpticCamera { get; init; }
            public ulong ActiveCamera { get; init; }
            public float Fov { get; init; }
            public float Aspect { get; init; }
            public float M14 { get; init; }
            public float M24 { get; init; }
            public float M44 { get; init; }
            public float RightX { get; init; }
            public float RightY { get; init; }
            public float RightZ { get; init; }
            public float UpX { get; init; }
            public float UpY { get; init; }
            public float UpZ { get; init; }
            public float TransX { get; init; }
            public float TransY { get; init; }
            public float TransZ { get; init; }
            public int ViewportW { get; init; }
            public int ViewportH { get; init; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetFovMagnitude(SKPoint point)
        {
            return Vector2.Distance(ViewportCenter.AsVector2(), point.AsVector2());
        }
    }
}
