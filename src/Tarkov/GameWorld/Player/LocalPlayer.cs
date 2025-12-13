/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * 
MIT License

Copyright (c) 2025 Lone DMA

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 *
*/

using System.Diagnostics;
using Collections.Pooled;
using LoneEftDmaRadar.Tarkov.Unity;
using LoneEftDmaRadar.Tarkov.Unity.Collections;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Misc;
using VmmSharpEx.Scatter;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Player
{
    public sealed class LocalPlayer : ClientPlayer
    {
        #region Wishlist Static Members

        /// <summary>
        /// Statische Sammlung aller Wishlist-Item-IDs.
        /// </summary>
        public static IReadOnlySet<string> WishlistItems => _wishlistItems;
        private static HashSet<string> _wishlistItems = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Lock _wishlistLock = new();

        #endregion

        /// <summary>
        /// Firearm Manager for tracking weapon/ammo/ballistics.
        /// </summary>
        public FirearmManager FirearmManager { get; private set; }
        
        private UnityTransform _lookRaycastTransform;

        /// <summary>
        /// Local Player's "look" position for accurate POV in aimview.
        /// Falls back to root position if unavailable.
        /// </summary>
        public Vector3 LookPosition => _lookRaycastTransform?.Position ?? this.Position;

        /// <summary>
        /// Public accessor for Hands Controller (used by FirearmManager).
        /// </summary>
        public new ulong HandsController
        {
            get => base.HandsController;
            private set => base.HandsController = value;
        }

        /// <summary>
        /// Spawn Point.
        /// </summary>
        public string EntryPoint { get; }

        /// <summary>
        /// Profile ID (if Player Scav). Used for Exfils.
        /// </summary>
        public string ProfileId { get; }

        /// <summary>
        /// Player name.
        /// </summary>
        public override string Name
        {
            get => "localPlayer";
            set { }
        }
        
        /// <summary>
        /// Player is Human-Controlled.
        /// </summary>
        public override bool IsHuman { get; }

        public LocalPlayer(ulong playerBase) : base(playerBase)
        {
            string classType = ObjectClass.ReadName(this);
            if (!(classType == "LocalPlayer" || classType == "ClientPlayer"))
                throw new ArgumentOutOfRangeException(nameof(classType));
            IsHuman = true;
            FirearmManager = new FirearmManager(this);

            if (IsPmc)
            {
                var entryPtr = Memory.ReadPtr(Info + Offsets.PlayerInfo.EntryPoint);
                EntryPoint = Memory.ReadUnicodeString(entryPtr);
            }
            else if (IsScav)
            {
                var profileIdPtr = Memory.ReadPtr(Profile + Offsets.Profile.Id);
                ProfileId = Memory.ReadUnicodeString(profileIdPtr);
            }
        }

        /// <summary>
        /// Update FirearmManager (call this before using weapon data).
        /// </summary>
        public void UpdateFirearmManager()
        {
            try
            {
                HandsController = Memory.ReadPtr(Base + Offsets.Player._handsController, false);
                FirearmManager?.Update();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[LocalPlayer] FirearmManager update failed: {ex}");
            }
        }

        /// <summary>
        /// Checks if LocalPlayer is Aiming (ADS).
        /// </summary>
        public bool CheckIfADS()
        {
            try
            {
                return Memory.ReadValue<bool>(PWA + Offsets.ProceduralWeaponAnimation._isAiming, false);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Refreshes the Wishlist from game memory.
        /// Should be called periodically from a background thread.
        /// </summary>
        public void RefreshWishlist()
        {
            try
            {
                var wishlistManager = Memory.ReadPtr(Profile + Offsets.Profile.WishlistManager);
                if (wishlistManager == 0)
                    return;

                var newWishlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Try _userItems first (0x28) - this is Dictionary<MongoID, int>
                var userItemsPtr = Memory.ReadPtr(wishlistManager + Offsets.WishlistManager.UserItems);
                
                if (userItemsPtr != 0)
                {
                    ReadDictionaryItems(userItemsPtr, newWishlist);
                }

                // Also try _wishlistItems (0x30) if no items found
                if (newWishlist.Count == 0)
                {
                    var wishlistItemsPtr = Memory.ReadPtr(wishlistManager + Offsets.WishlistManager.WishlistItems);
                    
                    if (wishlistItemsPtr != 0 && wishlistItemsPtr != userItemsPtr)
                    {
                        ReadDictionaryItems(wishlistItemsPtr, newWishlist);
                    }
                }

                lock (_wishlistLock)
                {
                    _wishlistItems = newWishlist;
                }
            }
            catch
            {
                // Silently fail - wishlist is optional feature
            }
        }

        /// <summary>
        /// Reads items from a Dictionary<MongoID, int> structure.
        /// </summary>
        private void ReadDictionaryItems(ulong dictPtr, HashSet<string> results)
        {
            try
            {
                // Try multiple count offsets
                var count1 = Memory.ReadValue<int>(dictPtr + 0x20);
                var count2 = Memory.ReadValue<int>(dictPtr + 0x40);
                var count = (count1 > 0 && count1 < 1000) ? count1 : count2;

                if (count <= 0 || count > 500)
                    return;

                var entriesPtr = Memory.ReadPtr(dictPtr + 0x18);
                if (entriesPtr == 0)
                    return;

                // Try different entry layouts
                int[] entrySizes = { 0x28, 0x30, 0x20, 0x38 };
                int[] keyOffsets = { 0x08, 0x10, 0x00 };
                
                foreach (var entrySize in entrySizes)
                {
                    foreach (var keyOffset in keyOffsets)
                    {
                        var tempResults = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var entriesStart = entriesPtr + 0x20; // Skip array header
                        
                        for (int i = 0; i < count; i++)
                        {
                            try
                            {
                                var entryAddr = entriesStart + (ulong)(i * entrySize);
                                // MongoID._stringId is at offset 0x10 within MongoID
                                var stringIdPtr = Memory.ReadPtr(entryAddr + (ulong)keyOffset + 0x10);
                                
                                if (stringIdPtr != 0 && stringIdPtr > 0x10000)
                                {
                                    var itemId = Memory.ReadUnicodeString(stringIdPtr, 64, true);
                                    if (!string.IsNullOrEmpty(itemId) && itemId.Length >= 20 && itemId.Length <= 30)
                                    {
                                        tempResults.Add(itemId);
                                    }
                                }
                            }
                            catch { }
                        }
                        
                        if (tempResults.Count > 0)
                        {
                            foreach (var id in tempResults)
                            {
                                results.Add(id);
                            }
                            return;
                        }
                    }
                }
            }
            catch
            {
                // Silently fail
            }
        }

        /// <summary>
        /// Initializes and updates the look raycast transform using memory scatter reads.
        /// Called when aimview is enabled.
        /// </summary>
        public override void OnRealtimeLoop(VmmScatter scatter)
        {
            base.OnRealtimeLoop(scatter);

            try
            {
                var transformPtr = Memory.ReadPtr(Base + Offsets.Player._playerLookRaycastTransform, false);

                if (transformPtr != 0x0)
                    _lookRaycastTransform = new UnityTransform(transformPtr);
                else
                    _lookRaycastTransform = null;
            }
            catch
            {
                _lookRaycastTransform = null;
            }
        }

        public override void OnValidateTransforms()
        {
            base.OnValidateTransforms();

            if (_lookRaycastTransform is null)
                return;

            try
            {
                var hierarchy = Memory.ReadPtr(_lookRaycastTransform.TransformInternal + UnitySDK.UnityOffsets.TransformAccess_HierarchyOffset);
                var vertexAddr = Memory.ReadPtr(hierarchy + UnitySDK.UnityOffsets.Hierarchy_VerticesOffset);
                if (vertexAddr == 0x0)
                {
                    DebugLogger.LogWarning("LookRaycast transform changed, updating cached transform...");
                    var transformPtr = Memory.ReadPtr(Base + Offsets.Player._playerLookRaycastTransform, false);
                    if (transformPtr != 0x0)
                        _lookRaycastTransform = new UnityTransform(transformPtr);
                    else
                        _lookRaycastTransform = null;
                }
            }
            catch
            {
                _lookRaycastTransform = null;
            }
        }
    }
}
