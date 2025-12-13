/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.UI.Misc;
using System.Runtime.InteropServices;
using VmmSharpEx;

namespace LoneEftDmaRadar.Tarkov.Unity.Structures
{
    /// <summary>
    /// Unity All Cameras. Contains an array of AllCameras.
    /// Uses signature-based lookup with fallback to hardcoded offset.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct AllCameras
    { 
        /// <summary>
        /// Looks up the Ptr of the All Cameras using signature scan with fallback.
        /// </summary>
        /// <param name="unityBase">UnityPlayer.dll module base address.</param>
        /// <returns>Pointer to AllCameras structure</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static ulong GetPtr(ulong unityBase)
        {
            try
            {
                try
                {
                    // Signature-based lookup (more future-proof)
                    // .text: 0000000180BA30C0 48 8B 05 39 D0 E4 00    mov rax, cs:AllCamerasPtr
                    // .text: 0000000180BA30C7 48 8B 08                mov rcx, [rax]
                    // .text: 0000000180BA30CA 49 8B 3C 0C             mov rdi, [r12+rcx]
                    const string signature = "48 8B 05 ? ? ? ? 48 8B 08 49 8B 3C 0C";
                    ulong allCamerasSig = Memory.FindSignature(signature);
                    allCamerasSig.ThrowIfInvalidVirtualAddress(nameof(allCamerasSig));
                    int rva = Memory.ReadValueEnsure<int>(allCamerasSig + 3);
                    var allCamerasPtr = Memory.ReadValueEnsure<VmmPointer>(allCamerasSig.AddRVA(7, rva));
                    allCamerasPtr.ThrowIfInvalid();
                    DebugLogger.LogDebug("AllCameras Located via Signature.");
                    return allCamerasPtr;
                }
                catch
                {
                    // Fallback to hardcoded offset
                    var allCamerasPtr = Memory.ReadValueEnsure<VmmPointer>(unityBase + UnitySDK.UnityOffsets.AllCameras);
                    allCamerasPtr.ThrowIfInvalid();
                    DebugLogger.LogDebug("AllCameras Located via Hardcoded Offset.");
                    return allCamerasPtr;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("ERROR Locating AllCameras Address", ex);
            }
        }
    }
}
