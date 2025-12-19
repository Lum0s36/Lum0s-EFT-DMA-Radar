/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.Unity;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Misc;
using System.Numerics;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Camera
{
    /// <summary>
    /// Handles camera search and validation logic.
    /// </summary>
    internal static class CameraSearch
    {
        /// <summary>
        /// Finds only the FPS camera during initialization.
        /// </summary>
        public static ulong FindFpsCamera(ulong listItemsPtr, int count)
        {
            ulong fpsCamera = 0;

            DebugLogger.LogDebug($"\n=== Searching for FPS Camera ===");
            DebugLogger.LogDebug($"List Items Ptr: 0x{listItemsPtr:X}");
            DebugLogger.LogDebug($"Camera Count: {count}");

            for (int i = 0; i < Math.Min(count, CameraConstants.MaxCameraSearchCount); i++)
            {
                try
                {
                    ulong cameraEntryAddr = listItemsPtr + (uint)(i * CameraConstants.CameraListStride);
                    var cameraPtr = Memory.ReadPtr(cameraEntryAddr, false);

                    if (cameraPtr == 0 || cameraPtr > CameraConstants.MaxValidPointer)
                        continue;

                    var gameObjectPtr = Memory.ReadPtr(cameraPtr + UnitySDK.UnityOffsets.Component_GameObjectOffset, false);
                    if (gameObjectPtr == 0 || gameObjectPtr > CameraConstants.MaxValidPointer)
                        continue;

                    var namePtr = Memory.ReadPtr(gameObjectPtr + UnitySDK.UnityOffsets.GameObject_NameOffset, false);
                    if (namePtr == 0 || namePtr > CameraConstants.MaxValidPointer)
                        continue;

                    var name = Memory.ReadUtf8String(namePtr, CameraConstants.MaxCameraNameLength, false);
                    if (string.IsNullOrEmpty(name) || name.Length < CameraConstants.MinCameraNameLength)
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
        /// Searches for potential optic cameras and adds them to the cache.
        /// </summary>
        public static void SearchOpticCameras(List<ulong> potentialOpticCameras, ref bool hasSearched)
        {
            try
            {
                if (hasSearched) return;

                var allCamerasPtr = AllCameras.GetPtr(Memory.UnityBase);
                if (allCamerasPtr == 0) return;

                var listItemsPtr = Memory.ReadPtr(allCamerasPtr + 0x0, false);
                var count = Memory.ReadValue<int>(allCamerasPtr + CameraConstants.CameraListCountOffset, false);

                DebugLogger.LogDebug($"Searching for potential optic cameras... ({count} cameras available)");

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        ulong cameraEntryAddr = listItemsPtr + (uint)(i * CameraConstants.CameraListStride);
                        var cameraPtr = Memory.ReadPtr(cameraEntryAddr, false);

                        if (cameraPtr == 0 || cameraPtr > CameraConstants.MaxValidPointer) continue;

                        var gameObjectPtr = Memory.ReadPtr(cameraPtr + UnitySDK.UnityOffsets.Component_GameObjectOffset, false);
                        if (gameObjectPtr == 0 || gameObjectPtr > CameraConstants.MaxValidPointer) continue;

                        var namePtr = Memory.ReadPtr(gameObjectPtr + UnitySDK.UnityOffsets.GameObject_NameOffset, false);
                        if (namePtr == 0 || namePtr > CameraConstants.MaxValidPointer) continue;

                        var name = Memory.ReadUtf8String(namePtr, CameraConstants.MaxCameraNameLength, false);
                        if (string.IsNullOrEmpty(name) || name.Length < CameraConstants.MinCameraNameLength) continue;

                        bool isPotentialOptic = (name.Contains("Clone", StringComparison.OrdinalIgnoreCase) ||
                                                 name.Contains("Optic", StringComparison.OrdinalIgnoreCase)) &&
                                                 name.Contains("Camera", StringComparison.OrdinalIgnoreCase);

                        if (isPotentialOptic)
                        {
                            DebugLogger.LogDebug($"Found potential Optic Camera: {name}");
                            potentialOpticCameras.Add(cameraPtr);
                        }
                    }
                    catch
                    {
                        // continue searching
                    }
                }

                hasSearched = true;
                DebugLogger.LogDebug($"Potential optic camera search complete: {potentialOpticCameras.Count} cameras cached");
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"SearchOpticCameras error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates an Optic Camera by checking if its view matrix contains valid data.
        /// </summary>
        public static bool ValidateOpticCameraMatrix(ulong cameraPtr)
        {
            try
            {
                var matrixAddress = GetMatrixAddress(cameraPtr, "Optic");
                var vm = Memory.ReadValue<Matrix4x4>(matrixAddress + UnitySDK.UnityOffsets.Camera_ViewMatrixOffset, false);

                if (Math.Abs(vm.M44) < CameraConstants.MinNonZeroValue)
                {
                    DebugLogger.LogDebug($"       Optic Camera validation failed: M44 is zero ({vm.M44})");
                    return false;
                }

                if (Math.Abs(vm.M41) < CameraConstants.MinNonZeroValue &&
                    Math.Abs(vm.M42) < CameraConstants.MinNonZeroValue &&
                    Math.Abs(vm.M43) < CameraConstants.MinNonZeroValue)
                {
                    DebugLogger.LogDebug($"       Optic Camera validation failed: Translation is all zeros");
                    return false;
                }

                float rightMag = MathF.Sqrt(vm.M11 * vm.M11 + vm.M12 * vm.M12 + vm.M13 * vm.M13);
                float upMag = MathF.Sqrt(vm.M21 * vm.M21 + vm.M22 * vm.M22 + vm.M23 * vm.M23);
                float fwdMag = MathF.Sqrt(vm.M31 * vm.M31 + vm.M32 * vm.M32 + vm.M33 * vm.M33);

                if (rightMag < CameraConstants.MinVectorMagnitude &&
                    upMag < CameraConstants.MinVectorMagnitude &&
                    fwdMag < CameraConstants.MinVectorMagnitude)
                {
                    DebugLogger.LogDebug($"       Optic Camera validation failed: All rotation magnitudes too small");
                    return false;
                }

                bool hasValidVectors = rightMag >= CameraConstants.MinVectorMagnitude && rightMag <= CameraConstants.MaxVectorMagnitude ||
                                       upMag >= CameraConstants.MinVectorMagnitude && upMag <= CameraConstants.MaxVectorMagnitude ||
                                       fwdMag >= CameraConstants.MinVectorMagnitude && fwdMag <= CameraConstants.MaxVectorMagnitude;

                if (!hasValidVectors)
                {
                    DebugLogger.LogDebug($"       Optic Camera validation failed: All vector magnitudes out of range");
                    return false;
                }

                DebugLogger.LogDebug($"       Optic Camera validation passed");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"       Optic Camera validation exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the matrix address for a camera.
        /// </summary>
        public static ulong GetMatrixAddress(ulong cameraPtr, string cameraType)
        {
            var gameObject = Memory.ReadPtr(cameraPtr + UnitySDK.UnityOffsets.Component_GameObjectOffset, false);

            if (gameObject == 0 || gameObject > CameraConstants.MaxValidPointer)
                throw new InvalidOperationException($"Invalid {cameraType} GameObject: 0x{gameObject:X}");

            var ptr1 = Memory.ReadPtr(gameObject + UnitySDK.UnityOffsets.GameObject_ComponentsOffset, false);

            if (ptr1 == 0 || ptr1 > CameraConstants.MaxValidPointer)
                throw new InvalidOperationException($"Invalid {cameraType} Ptr1: 0x{ptr1:X}");

            var matrixAddress = Memory.ReadPtr(ptr1 + CameraConstants.CameraMatrixComponentsOffset, false);

            if (matrixAddress == 0 || matrixAddress > CameraConstants.MaxValidPointer)
                throw new InvalidOperationException($"Invalid {cameraType} matrixAddress: 0x{matrixAddress:X}");

            return matrixAddress;
        }

        /// <summary>
        /// Verifies and logs a view matrix for debugging.
        /// </summary>
        public static void VerifyViewMatrix(ulong matrixAddress, string name)
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
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"ERROR verifying ViewMatrix for {name}: {ex}");
            }
        }
    }
}
