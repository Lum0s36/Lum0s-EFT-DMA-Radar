/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * 
 * Quest Helper: Credit to LONE for the foundational implementation
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

using Collections.Pooled;
using LoneEftDmaRadar.Tarkov.Unity.Collections;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Misc;
using System.Collections.Frozen;
using System.Diagnostics;
using SDK;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Quests
{
    public sealed class QuestManager
    {
        private readonly ulong _profile;
        private DateTime _lastRefresh = DateTime.MinValue;
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);
        
        /// <summary>
        /// Cached condition counters from Profile.TaskConditionCounters
        /// Key: MongoID string, Value: (current count, target count)
        /// </summary>
        private readonly ConcurrentDictionary<string, (int CurrentCount, int TargetCount)> _conditionCounters = new(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// Cached TaskConditionCounter pointers for fast value updates.
        /// Key: condition ID, Value: (counterPtr, targetCount)
        /// </summary>
        private readonly ConcurrentDictionary<string, (ulong CounterPtr, int TargetCount)> _counterPointers = new(StringComparer.OrdinalIgnoreCase);
        private bool _countersInitialized = false;

        public QuestManager(ulong profile)
        {
            _profile = profile;
        }

        private readonly ConcurrentDictionary<string, QuestEntry> _quests = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// All current quests.
        /// </summary>
        public IReadOnlyDictionary<string, QuestEntry> Quests => _quests;

        private readonly ConcurrentDictionary<string, byte> _items = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// All item BSG ID's that we need to pickup.
        /// </summary>
        public IReadOnlyDictionary<string, byte> ItemConditions => _items;

        private readonly ConcurrentDictionary<string, QuestLocation> _locations = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// All locations that we need to visit.
        /// </summary>
        public IReadOnlyDictionary<string, QuestLocation> LocationConditions => _locations;

        /// <summary>
        /// Map Identifier of Current Map.
        /// </summary>
        private static string MapID
        {
            get
            {
                var id = Memory.MapID;
                id ??= "MAPDEFAULT";
                return id;
            }
        }

        public void Refresh(CancellationToken ct)
        {
            try
            {
                // Rate limiting
                if (DateTime.UtcNow - _lastRefresh < RefreshInterval)
                    return;
                _lastRefresh = DateTime.UtcNow;

                using var masterQuests = new PooledSet<string>(StringComparer.OrdinalIgnoreCase);
                using var masterItems = new PooledSet<string>(StringComparer.OrdinalIgnoreCase);
                using var masterLocations = new PooledSet<string>(StringComparer.OrdinalIgnoreCase);

                // Read condition counters from Profile.TaskConditionCounters (Dictionary<MongoID, TaskConditionCounter>)
                ReadConditionCountersFromProfile();

                var questsData = Memory.ReadPtr(_profile + Offsets.Profile.QuestsData);
                using var questsDataList = UnityList<ulong>.Create(questsData, false);

                foreach (var qDataEntry in questsDataList)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        // qDataEntry should be public class QuestStatusData : Object
                        var qStatus = Memory.ReadValue<int>(qDataEntry + Offsets.QuestStatusData.Status);
                        if (qStatus != 2) // started
                            continue;

                        var qIdPtr = Memory.ReadPtr(qDataEntry + Offsets.QuestStatusData.Id);
                        var qId = Memory.ReadUnicodeString(qIdPtr, 64, false);

                        // qID should be Task ID
                        if (string.IsNullOrEmpty(qId) || !TarkovDataManager.TaskData.TryGetValue(qId, out var task))
                            continue;

                        masterQuests.Add(qId);
                        var questEntry = _quests.GetOrAdd(qId, id => new QuestEntry(id));

                        // Read completed conditions from QuestStatusData.CompletedConditions HashSet
                        var completedConditionsPtr = Memory.ReadPtr(qDataEntry + Offsets.QuestStatusData.CompletedConditions);
                        using var completedConditions = new PooledList<string>();

                        if (completedConditionsPtr != 0)
                        {
                            try
                            {
                                ReadCompletedConditionsHashSet(completedConditionsPtr, completedConditions);
                            }
                            catch (Exception ex)
                            {
                                DebugLogger.LogDebug($"[QuestManager] Error reading CompletedConditions for {qId}: {ex.Message}");
                            }
                        }
                        
                        // Update quest entry with completed conditions
                        questEntry.UpdateCompletedConditions(completedConditions);
                        
                        // Update quest entry with condition counters from profile
                        if (task.Objectives != null)
                        {
                            using var counters = new PooledList<KeyValuePair<string, (int CurrentCount, int TargetCount)>>();
                            foreach (var obj in task.Objectives)
                            {
                                if (!string.IsNullOrEmpty(obj.Id))
                                {
                                    // First check if we have a counter from profile (with both current and target from memory)
                                    if (_conditionCounters.TryGetValue(obj.Id, out var count))
                                    {
                                        // Use target from memory if available, otherwise use API value as fallback
                                        int targetCount = count.TargetCount > 0 ? count.TargetCount : obj.Count;
                                        counters.Add(new KeyValuePair<string, (int, int)>(obj.Id, (count.CurrentCount, targetCount)));
                                        
                                        // Debug log the match
                                        DebugLogger.LogDebug($"[QuestManager] Matched objective '{obj.Id}' ({obj.Description?.Substring(0, Math.Min(30, obj.Description?.Length ?? 0))}...) = {count.CurrentCount}/{targetCount}");
                                    }
                                    // If objective is completed, set count to target
                                    else if (completedConditions.Contains(obj.Id) && obj.Count > 0)
                                    {
                                        counters.Add(new KeyValuePair<string, (int, int)>(obj.Id, (obj.Count, obj.Count)));
                                    }
                                }
                            }
                            
                            if (counters.Count > 0)
                            {
                                DebugLogger.LogDebug($"[QuestManager] Quest '{task.Name}': Updating {counters.Count} counters from memory");
                            }
                            
                            questEntry.UpdateConditionCounters(counters);
                        }

                        if (App.Config.QuestHelper.BlacklistedQuests.ContainsKey(qId))
                            continue; // Log the quest but dont get any conditions

                        // Convert to PooledSet for FilterConditions
                        using var completedSet = new PooledSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var c in completedConditions)
                            completedSet.Add(c);

                        FilterConditions(task, qId, completedSet, masterItems, masterLocations);
                    }
                    catch
                    {
                        // Skip invalid quest entries
                    }
                }

                // Remove stale Quests/Items/Locations
                foreach (var oldQuest in _quests)
                {
                    if (!masterQuests.Contains(oldQuest.Key))
                        _quests.TryRemove(oldQuest.Key, out _);
                }
                foreach (var oldItem in _items)
                {
                    if (!masterItems.Contains(oldItem.Key))
                        _items.TryRemove(oldItem.Key, out _);
                }
                foreach (var oldLoc in _locations.Keys)
                {
                    if (!masterLocations.Contains(oldLoc))
                        _locations.TryRemove(oldLoc, out _);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[QuestManager] CRITICAL ERROR: {ex}");
            }
        }
        
        /// <summary>
        /// Read completed conditions from HashSet<MongoID> manually.
        /// HashSet layout in Unity/Mono:
        /// +0x18 = _slots array (contains entries)
        /// +0x3C = _count
        /// </summary>
        private void ReadCompletedConditionsHashSet(ulong hashSetPtr, PooledList<string> results)
        {
            // HashSet<MongoID> layout:
            // +0x18 = Slot[] _slots
            // +0x3C = int _count
            
            var count = Memory.ReadValue<int>(hashSetPtr + 0x3C);
            if (count <= 0 || count > 100)
                return;
            
            var slotsArrayPtr = Memory.ReadPtr(hashSetPtr + 0x18);
            if (slotsArrayPtr == 0)
                return;
            
            // Array header: length at +0x18
            var slotsArrayLength = Memory.ReadValue<int>(slotsArrayPtr + 0x18);
            if (slotsArrayLength <= 0 || slotsArrayLength > 200)
                return;
            
            // Slot struct for HashSet<MongoID>:
            // struct Slot {
            //     int hashCode;     // +0x00
            //     int next;         // +0x04
            //     MongoID value;    // +0x08 (0x18 bytes)
            // }
            // Total = 0x20 (32 bytes) with padding
            
            // However, with Pack=1 it would be 0x08 + 0x18 = 0x20
            // Let's try different slot sizes
            int[] slotSizes = { 0x20, 0x28, 0x30 };
            
            foreach (var slotSize in slotSizes)
            {
                results.Clear();
                var slotsStart = slotsArrayPtr + 0x20; // Skip array header
                int foundCount = 0;
                
                for (int i = 0; i < slotsArrayLength && i < 100; i++)
                {
                    try
                    {
                        var slotAddr = slotsStart + (ulong)(i * slotSize);
                        var hashCode = Memory.ReadValue<int>(slotAddr);
                        
                        // In .NET HashSet, hashCode >= 0 means slot is used
                        if (hashCode < 0)
                            continue;
                        
                        // MongoID._stringId is at offset 0x10 within MongoID
                        // MongoID starts at slot offset 0x08
                        var mongoIdOffset = slotAddr + 0x08;
                        var stringIdPtr = Memory.ReadPtr(mongoIdOffset + 0x10);
                        
                        if (stringIdPtr != 0 && stringIdPtr > 0x10000)
                        {
                            var conditionId = Memory.ReadUnicodeString(stringIdPtr, 64, true);
                            if (!string.IsNullOrEmpty(conditionId) && conditionId.Length >= 10 && conditionId.Length <= 50)
                            {
                                results.Add(conditionId);
                                foundCount++;
                                DebugLogger.LogDebug($"[QuestManager] Found completed condition: {conditionId}");
                            }
                        }
                    }
                    catch
                    {
                        // Skip invalid slots
                    }
                }
                
                // If we found the expected number of items, we have the right slot size
                if (foundCount > 0 && foundCount >= count / 2)
                {
                    DebugLogger.LogDebug($"[QuestManager] Successfully read {foundCount} completed conditions with slot size {slotSize:X}");
                    return;
                }
            }
            
            // Fallback: try using UnityHashSet but log the failure
            DebugLogger.LogDebug("[QuestManager] Manual HashSet reading failed, results may be incomplete");
        }
        
        /// <summary>
        /// Read TaskConditionCounters from Profile.
        /// On first call, caches the counter pointers.
        /// On subsequent calls, re-initializes if needed or just reads current values.
        /// </summary>
        private void ReadConditionCountersFromProfile()
        {
            try
            {
                // Check if dictionary count has changed - if so, reinitialize
                var countersPtr = Memory.ReadPtr(_profile + Offsets.Profile.TaskConditionCounters, false);
                if (countersPtr != 0)
                {
                    var countPtr = countersPtr + 0x40; // Dictionary _count field
                    var currentCount = Memory.ReadValue<int>(countPtr, false);
                    
                    // If count changed, we need to reinitialize to pick up new counters
                    if (_countersInitialized && currentCount != _counterPointers.Count)
                    {
                        DebugLogger.LogDebug($"[QuestManager] Counter count changed ({_counterPointers.Count} -> {currentCount}), reinitializing...");
                        _countersInitialized = false;
                    }
                }
                
                // If we have cached pointers and count hasn't changed, just refresh values
                if (_countersInitialized && _counterPointers.Count > 0)
                {
                    RefreshCounterValuesFromCache();
                    return;
                }
                
                // First time or count changed - need to discover all counter pointers
                InitializeCounterPointers();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[QuestManager] Error reading condition counters: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Refresh counter values from cached pointers (fast path).
        /// </summary>
        private void RefreshCounterValuesFromCache()
        {
            _conditionCounters.Clear();
            int refreshedCount = 0;
            
            foreach (var kvp in _counterPointers)
            {
                try
                {
                    var conditionId = kvp.Key;
                    var (counterPtr, targetValue) = kvp.Value;
                    
                    // Read current Value (int at offset 0x40 in TaskConditionCounter) - ALWAYS fresh
                    var currentValue = Memory.ReadValue<int>(counterPtr + Offsets.TaskConditionCounter.Value, false);
                    
                    if (currentValue >= 0 && currentValue < 10000)
                    {
                        _conditionCounters[conditionId] = (currentValue, targetValue);
                        refreshedCount++;
                    }
                }
                catch
                {
                    // Counter may have become invalid - skip for now
                }
            }
            
            if (refreshedCount > 0)
            {
                DebugLogger.LogDebug($"[QuestManager] Refreshed {refreshedCount} counter values from cache");
            }
        }
        
        /// <summary>
        /// Initialize counter pointers by reading the dictionary structure.
        /// </summary>
        private void InitializeCounterPointers()
        {
            var countersPtr = Memory.ReadPtr(_profile + Offsets.Profile.TaskConditionCounters, false);
            if (countersPtr == 0)
            {
                DebugLogger.LogDebug("[QuestManager] TaskConditionCounters pointer is null");
                return;
            }

            var entriesPtr = Memory.ReadPtr(countersPtr + 0x18, false);
            if (entriesPtr == 0)
            {
                DebugLogger.LogDebug("[QuestManager] Entries array pointer is null");
                return;
            }
            
            var arrayLength = Memory.ReadValue<int>(entriesPtr + 0x18, false);
            
            if (arrayLength <= 0 || arrayLength > 500)
            {
                DebugLogger.LogDebug($"[QuestManager] Invalid array length: {arrayLength}");
                return;
            }
            
            _counterPointers.Clear();
            _conditionCounters.Clear();
            int foundCounters = 0;
            
            const int entrySize = 0x28;
            const int hashCodeOffset = 0x00;
            const int keyOffset = 0x08;
            const int valueOffset = 0x20;
            
            var entriesStart = entriesPtr + 0x20;
            
            for (int i = 0; i < arrayLength && i < 300; i++)
            {
                try
                {
                    var entryAddr = entriesStart + (uint)(i * entrySize);
                    
                    var hashCode = Memory.ReadValue<int>(entryAddr + hashCodeOffset, false);
                    if (hashCode < 0)
                        continue;
                    
                    var mongoId = Memory.ReadValue<MongoID>(entryAddr + keyOffset, false);
                    var conditionId = mongoId.ReadString(128, false);
                    
                    if (string.IsNullOrEmpty(conditionId))
                        continue;
                    
                    var counterPtr = Memory.ReadPtr(entryAddr + valueOffset, false);
                    if (counterPtr == 0)
                        continue;
                    
                    // Read current Value
                    var currentValue = Memory.ReadValue<int>(counterPtr + Offsets.TaskConditionCounter.Value, false);
                    
                    // Read target Value from Template
                    int targetValue = 0;
                    try
                    {
                        var templatePtr = Memory.ReadPtr(counterPtr + Offsets.TaskConditionCounter.Template, false);
                        if (templatePtr != 0)
                        {
                            var floatValue = Memory.ReadValue<float>(templatePtr + Offsets.Condition.Value, false);
                            targetValue = (int)floatValue;
                            
                            if (targetValue < 0 || targetValue > 10000)
                                targetValue = 0;
                        }
                    }
                    catch
                    {
                        targetValue = 0;
                    }
                    
                    if (currentValue >= 0 && currentValue < 10000)
                    {
                        // Cache the pointer and target for future fast reads
                        _counterPointers[conditionId] = (counterPtr, targetValue);
                        _conditionCounters[conditionId] = (currentValue, targetValue);
                        foundCounters++;
                        
                        if (foundCounters <= 5)
                        {
                            DebugLogger.LogDebug($"[QuestManager] Counter initialized: {conditionId} = {currentValue}/{targetValue}");
                        }
                    }
                }
                catch
                {
                    // Skip invalid entries
                }
            }
            
            if (foundCounters > 0)
            {
                _countersInitialized = true;
                DebugLogger.LogDebug($"[QuestManager] Initialized {foundCounters} counter pointers for fast refresh");
            }
        }
        
        private static readonly FrozenSet<QuestObjectiveType> _skipObjectiveTypes = new HashSet<QuestObjectiveType>
        {
            QuestObjectiveType.BuildWeapon,
            QuestObjectiveType.GiveQuestItem,
            QuestObjectiveType.Extract,
            QuestObjectiveType.Shoot,
            QuestObjectiveType.TraderLevel,
            QuestObjectiveType.GiveItem
        }.ToFrozenSet();

        private void FilterConditions(
            TarkovDataManager.TaskElement task,
            string questId,
            PooledSet<string> completedConditions,
            PooledSet<string> masterItems,
            PooledSet<string> masterLocations)
        {
            if (task?.Objectives is null)
                return;

            foreach (var objective in task.Objectives)
            {
                try
                {
                    if (objective is null)
                        continue;

                    // Skip objectives that are already completed (by condition id)
                    if (!string.IsNullOrEmpty(objective.Id) && completedConditions.Contains(objective.Id))
                        continue;

                    if (_skipObjectiveTypes.Contains(objective.Type))
                        continue;

                    // Item Pickup Objectives - Track item IDs for highlighting in LootManager
                    // Note: For FindQuestItem, we do NOT create zone markers here.
                    // Quest items (like Bronze Pocket Watch) are tracked by LootManager via
                    // the ItemTemplate.QuestItem flag and their real in-raid position.
                    if (objective.Type == QuestObjectiveType.FindQuestItem)
                    {
                        if (objective.QuestItem?.Id is not null)
                        {
                            masterItems.Add(objective.QuestItem.Id);
                            _ = _items.GetOrAdd(objective.QuestItem.Id, 0);
                        }
                        // Skip adding zone markers - quest items have live positions from LootManager
                        continue;
                    }
                    else if (objective.Type == QuestObjectiveType.FindItem)
                    {
                        if (objective.Item?.Id is not null)
                        {
                            masterItems.Add(objective.Item.Id);
                            _ = _items.GetOrAdd(objective.Item.Id, 0);
                        }
                        // For regular items, still check for zones (some quests have specific pickup locations)
                    }
                    
                    // Location Visit Objectives: visit, mark, plantItem, plantQuestItem
                    // These objectives have fixed zone locations that make sense to show
                    if (objective.Type == QuestObjectiveType.Visit
                        || objective.Type == QuestObjectiveType.Mark
                        || objective.Type == QuestObjectiveType.PlantItem
                        || objective.Type == QuestObjectiveType.PlantQuestItem)
                    {
                        if (objective.Zones is not null && objective.Zones.Count > 0)
                        {
                            if (TarkovDataManager.TaskZones.TryGetValue(MapID, out var zonesForMap))
                            {
                                foreach (var zone in objective.Zones)
                                {
                                    if (zone?.Id is string zoneId && zonesForMap.TryGetValue(zoneId, out var pos))
                                    {
                                        // Make a stable key for this quest-objective-zone triple
                                        var locKey = $"{questId}:{objective.Id}:{zoneId}";
                                        _locations.GetOrAdd(locKey, _ => new QuestLocation(questId, objective.Id, pos));
                                        masterLocations.Add(locKey);
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Skip invalid objectives
                }
            }
        }

        /// <summary>
        /// Check if an item ID is required for an active quest.
        /// </summary>
        public bool IsQuestItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return false;
            return _items.ContainsKey(itemId);
        }
        
        /// <summary>
        /// Get objective progress for a specific quest and objective.
        /// </summary>
        public (bool isCompleted, int currentCount) GetObjectiveProgress(string questId, string objectiveId)
        {
            if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(objectiveId))
                return (false, 0);
                
            if (_quests.TryGetValue(questId, out var quest))
            {
                var isCompleted = quest.IsObjectiveCompleted(objectiveId);
                var currentCount = quest.GetObjectiveProgress(objectiveId);
                return (isCompleted, currentCount);
            }
            
            return (false, 0);
        }
    }
}
