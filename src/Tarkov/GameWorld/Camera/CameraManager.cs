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
        static CameraManager()
        {
            MemDMA.ProcessStarting += MemDMA_ProcessStarting;
            MemDMA.ProcessStopped += MemDMA_ProcessStopped;
        }

        private static void MemDMA_ProcessStarting(object sender, EventArgs e) { }
        private static void MemDMA_ProcessStopped(object sender, EventArgs e) { }

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
                    }
                    else
                    {
                        width = (int)App.Config.UI.Resolution.Width;
                        height = (int)App.Config.UI.Resolution.Height;
                    }
                }

                if (width <= 0 || height <= 0)
                {
                    width = CameraConstants.DefaultViewportWidth;
                    height = CameraConstants.DefaultViewportHeight;
                }

                Viewport = new Rectangle(0, 0, width, height);
            }
        }

        public static bool WorldToScreen(ref readonly Vector3 worldPos, out SKPoint scrPos, bool onScreenCheck = false, bool useTolerance = false)
        {
            try
            {
                float w = Vector3.Dot(_viewMatrix.Translation, worldPos) + _viewMatrix.M44;

                if (w < CameraConstants.MinProjectionW)
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
                    int left = useTolerance ? Viewport.Left - CameraConstants.ViewportTolerance : Viewport.Left;
                    int right = useTolerance ? Viewport.Right + CameraConstants.ViewportTolerance : Viewport.Right;
                    int top = useTolerance ? Viewport.Top - CameraConstants.ViewportTolerance : Viewport.Top;
                    int bottom = useTolerance ? Viewport.Bottom + CameraConstants.ViewportTolerance : Viewport.Bottom;

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
            if (IsInitialized) return;

            _potentialOpticCameras.Clear();
            _hasSearchedForPotentialCameras = false;
            _useFpsCameraForCurrentAds = false;

            try
            {
                DebugLogger.LogDebug("=== CameraManager Initialization ===");

                var allCamerasPtr = AllCameras.GetPtr(Memory.UnityBase);

                if (allCamerasPtr == 0)
                    throw new InvalidOperationException("AllCameras pointer is NULL - offset may be outdated");

                if (allCamerasPtr > CameraConstants.MaxValidPointer)
                    throw new InvalidOperationException($"Invalid AllCameras pointer: 0x{allCamerasPtr:X}");

                var listItemsPtr = Memory.ReadPtr(allCamerasPtr + 0x0, false);
                var count = Memory.ReadValue<int>(allCamerasPtr + CameraConstants.CameraListCountOffset, false);

                if (listItemsPtr == 0)
                    throw new InvalidOperationException("Camera list items pointer is NULL");

                if (count <= 0)
                    throw new InvalidOperationException($"No cameras in list (count: {count})");

                var fps = CameraSearch.FindFpsCamera(listItemsPtr, count);

                if (fps == 0)
                    throw new InvalidOperationException("Could not find required FPS Camera!");

                FPSCamera = fps;
                _fpsMatrixAddress = CameraSearch.GetMatrixAddress(FPSCamera, "FPS");

                FPSCameraPtr = FPSCamera;
                OpticCameraPtr = 0;
                ActiveCameraPtr = 0;
                _opticMatrixAddress = 0;

                CameraSearch.VerifyViewMatrix(_fpsMatrixAddress, "FPS");

                IsInitialized = true;
                DebugLogger.LogDebug("=== CameraManager Initialization Complete ===\n");
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($" CameraManager initialization failed: {ex}");
                throw;
            }
        }

        private bool CheckIfScoped(LocalPlayer localPlayer)
        {
            try
            {
                if (localPlayer is null || !OpticCameraActive)
                    return false;

                var opticsPtr = Memory.ReadPtr(localPlayer.PWA + Offsets.ProceduralWeaponAnimation._optics);
                using var optics = UnityList<VmmPointer>.Create(opticsPtr, true);

                if (optics.Count > 0)
                {
                    var pSightComponent = Memory.ReadPtr(optics[0] + Offsets.SightNBone.Mod);
                    var sightComponent = Memory.ReadValue<SightComponent>(pSightComponent);

                    if (sightComponent.ScopeZoomValue != 0f)
                        return sightComponent.ScopeZoomValue > CameraConstants.MinScopeZoom;

                    float zoomLevel = sightComponent.GetZoomLevel();
                    return zoomLevel > CameraConstants.MinScopeZoom;
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

                if (!IsADS)
                    _useFpsCameraForCurrentAds = false;

                if (IsADS && OpticCameraPtr == 0 && !_useFpsCameraForCurrentAds)
                {
                    if (_hasSearchedForPotentialCameras && _potentialOpticCameras.Count > 0)
                    {
                        if (!ValidateOpticCameras())
                            _useFpsCameraForCurrentAds = true;
                    }
                    else
                    {
                        CameraSearch.SearchOpticCameras(_potentialOpticCameras, ref _hasSearchedForPotentialCameras);
                        if (_potentialOpticCameras.Count > 0)
                        {
                            if (!ValidateOpticCameras())
                                _useFpsCameraForCurrentAds = true;
                        }
                        else
                        {
                            _useFpsCameraForCurrentAds = true;
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
                    if (s.ReadValue<Matrix4x4>(vmAddr, out var vm) && !Unsafe.IsNullRef(ref vm))
                        _viewMatrix.Update(ref vm);
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

        private bool ValidateOpticCameras()
        {
            foreach (var cameraPtr in _potentialOpticCameras)
            {
                if (CameraSearch.ValidateOpticCameraMatrix(cameraPtr))
                {
                    OpticCameraPtr = cameraPtr;
                    _opticMatrixAddress = CameraSearch.GetMatrixAddress(cameraPtr, "Optic");
                    CameraSearch.VerifyViewMatrix(_opticMatrixAddress, "Optic");
                    return true;
                }
            }
            return false;
        }

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
