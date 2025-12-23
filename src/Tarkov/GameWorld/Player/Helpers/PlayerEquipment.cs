using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.Unity.Collections;
using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.Web.TarkovDev.Data;
using System.Collections.Frozen;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers
{
    public sealed class PlayerEquipment
    {
        private static readonly FrozenSet<string> _skipSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SecuredContainer", "Dogtag", "Compass", "ArmBand", "Eyewear", "Pockets"
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ulong> _slots = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, TarkovMarketItem> _items = new(StringComparer.OrdinalIgnoreCase);
        private readonly ObservedPlayer _player;
        private bool _inited;
        
        // Track last known item IDs per slot to detect changes
        private readonly ConcurrentDictionary<string, string> _lastItemIds = new(StringComparer.OrdinalIgnoreCase);
        
        // Track item version numbers to detect when items are removed from corpses
        private readonly ConcurrentDictionary<string, int> _itemVersions = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Player's eqiuipped gear by slot.
        /// </summary>
        public IReadOnlyDictionary<string, TarkovMarketItem> Items => _items;
        /// <summary>
        /// Player's total equipment flea price value.
        /// </summary>
        public int Value => (int)_items.Values.Sum(i => i.FleaPrice);

        public PlayerEquipment(ObservedPlayer player)
        {
            _player = player;
            Task.Run(InitAsnyc); // Lazy init
        }

        private async Task InitAsnyc()
        {
            for (int i = 0; i < PlayerConstants.EquipmentInitRetryCount; i++)
            {
                try
                {
                    var inventorycontroller = Memory.ReadPtr(_player.InventoryControllerAddr);
                    var inventory = Memory.ReadPtr(inventorycontroller + Offsets.InventoryController.Inventory);
                    var equipment = Memory.ReadPtr(inventory + Offsets.Inventory.Equipment);
                    var slotsPtr = Memory.ReadPtr(equipment + Offsets.InventoryEquipment._cachedSlots);
                    using var slotsArray = UnityArray<ulong>.Create(slotsPtr, true);
                    ArgumentOutOfRangeException.ThrowIfLessThan(slotsArray.Count, PlayerConstants.MinEquipmentSlotCount);

                    foreach (var slotPtr in slotsArray)
                    {
                        var namePtr = Memory.ReadPtr(slotPtr + Offsets.Slot.ID);
                        var name = Memory.ReadUnicodeString(namePtr);
                        if (_skipSlots.Contains(name))
                            continue;
                        _slots.TryAdd(name, slotPtr);
                    }

                    Refresh(checkInit: false);
                    _inited = true;
                    return;
                }
                catch (Exception ex)
                {
                    DebugLogger.LogDebug($"Error initializing Player Equipment for '{_player.Name}': {ex}");
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromSeconds(PlayerConstants.EquipmentInitRetryDelaySeconds));
                }
            }
        }

        public void Refresh(bool checkInit = true)
        {
            if (checkInit && !_inited)
                return;
            foreach (var slot in _slots)
            {
                try
                {
                    if (_player.IsPmc && slot.Key == "Scabbard")
                    {
                        continue; // skip pmc scabbard
                    }
                    
                    // Read the ContainedItem pointer directly as ulong first
                    // If it's 0 or invalid, the slot is empty
                    var containedItemPtr = Memory.ReadValue<ulong>(slot.Value + Offsets.Slot.ContainedItem, false);
                    
                    // Check if slot is empty (null pointer)
                    if (containedItemPtr == 0 || !MemDMA.IsValidVirtualAddress(containedItemPtr))
                    {
                        // Slot is empty - remove any cached item
                        if (_items.TryRemove(slot.Key, out _))
                        {
                            _lastItemIds.TryRemove(slot.Key, out _);
                            _itemVersions.TryRemove(slot.Key, out _);
                        }
                        continue;
                    }
                    
                    // Read template from contained item
                    var inventoryTemplatePtr = Memory.ReadValue<ulong>(containedItemPtr + Offsets.LootItem.Template, false);
                    if (inventoryTemplatePtr == 0 || !MemDMA.IsValidVirtualAddress(inventoryTemplatePtr))
                    {
                        if (_items.TryRemove(slot.Key, out _))
                        {
                            _lastItemIds.TryRemove(slot.Key, out _);
                            _itemVersions.TryRemove(slot.Key, out _);
                        }
                        continue;
                    }
                    
                    // Read item version - this changes when item is modified/moved
                    var itemVersion = Memory.ReadValue<int>(containedItemPtr + Offsets.LootItem.Version, false);
                    
                    var mongoId = Memory.ReadValue<MongoID>(inventoryTemplatePtr + Offsets.ItemTemplate._id, false);
                    var id = mongoId.ReadString();
                    
                    // Validate the ID looks reasonable (BSG IDs are 24 char hex strings)
                    if (string.IsNullOrEmpty(id) || id.Length < 20 || id.Length > 30)
                    {
                        // Invalid ID - slot might be corrupted or item removed
                        if (_items.TryRemove(slot.Key, out _))
                        {
                            _lastItemIds.TryRemove(slot.Key, out _);
                            _itemVersions.TryRemove(slot.Key, out _);
                        }
                        continue;
                    }
                    
                    if (TarkovDataManager.AllItems.TryGetValue(id, out var item))
                    {
                        _items[slot.Key] = item;
                        _lastItemIds[slot.Key] = id;
                        _itemVersions[slot.Key] = itemVersion;
                    }
                    else
                    {
                        if (_items.TryRemove(slot.Key, out _))
                        {
                            _lastItemIds.TryRemove(slot.Key, out _);
                            _itemVersions.TryRemove(slot.Key, out _);
                        }
                    }
                }
                catch
                {
                    _items.TryRemove(slot.Key, out _);
                    _lastItemIds.TryRemove(slot.Key, out _);
                    _itemVersions.TryRemove(slot.Key, out _);
                }
            }
        }
    }
}