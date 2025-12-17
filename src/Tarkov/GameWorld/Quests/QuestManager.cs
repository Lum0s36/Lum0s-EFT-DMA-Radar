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
        /// Key: MongoID string, Value: current count
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _conditionCounters = new(StringComparer.OrdinalIgnoreCase);

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
                                using var completedHS = UnityHashSet<MongoID>.Create(completedConditionsPtr, true);
                                foreach (var c in completedHS)
                                {
                                    var completedCond = c.Value.ReadString();
                                    if (!string.IsNullOrEmpty(completedCond))
                                    {
                                        completedConditions.Add(completedCond);
                                        DebugLogger.LogDebug($"[QuestManager] Quest {qId}: Completed condition {completedCond}");
                                    }
                                }
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
                            using var counters = new PooledList<KeyValuePair<string, int>>();
                            foreach (var obj in task.Objectives)
                            {
                                if (!string.IsNullOrEmpty(obj.Id))
                                {
                                    // First check if we have a counter from profile
                                    if (_conditionCounters.TryGetValue(obj.Id, out var count))
                                    {
                                        counters.Add(new KeyValuePair<string, int>(obj.Id, count));
                                    }
                                    // If objective is completed, set count to target
                                    else if (completedConditions.Contains(obj.Id) && obj.Count > 0)
                                    {
                                        counters.Add(new KeyValuePair<string, int>(obj.Id, obj.Count));
                                    }
                                }
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
        /// Read TaskConditionCounters from Profile.
        /// Profile.TaskConditionCounters is Dictionary<MongoID, TaskConditionCounter> at offset 0x90
        /// </summary>
        private void ReadConditionCountersFromProfile()
        {
            try
            {
                var countersPtr = Memory.ReadPtr(_profile + Offsets.Profile.TaskConditionCounters);
                if (countersPtr == 0)
                {
                    DebugLogger.LogDebug("[QuestManager] TaskConditionCounters pointer is null");
                    return;
                }

                // Unity/Mono Dictionary<TKey, TValue> layout:
                // +0x10 = buckets array
                // +0x18 = entries array  
                // +0x20 = count (but we need freeCount subtracted)
                // +0x28 = freeList
                // +0x2C = freeCount
                // +0x30 = version
                // Actual count = entries count from array
                
                var entriesPtr = Memory.ReadPtr(countersPtr + 0x18);
                if (entriesPtr == 0)
                {
                    DebugLogger.LogDebug("[QuestManager] Entries pointer is null");
                    return;
                }
                
                // Read array length from entries array header (+0x18 in managed array)
                var arrayLength = Memory.ReadValue<int>(entriesPtr + 0x18);
                
                if (arrayLength <= 0 || arrayLength > 500)
                {
                    DebugLogger.LogDebug($"[QuestManager] Invalid array length: {arrayLength}");
                    return;
                }
                
                _conditionCounters.Clear();
                int foundCounters = 0;
                
                // Dictionary entry structure for Dictionary<MongoID, TaskConditionCounter>:
                // struct Entry {
                //     int hashCode;      // +0x00 (4 bytes)
                //     int next;          // +0x04 (4 bytes)
                //     MongoID key;       // +0x08 (0x18 bytes = 24 bytes)
                //     TaskConditionCounter value; // +0x20 (8 bytes pointer)
                // }
                // Total entry size = 0x28 (40 bytes)
                
                const int entrySize = 0x28;
                const int hashCodeOffset = 0x00;
                const int keyOffset = 0x08;
                const int valueOffset = 0x20;
                
                // Entries array starts at +0x20 (after array header)
                var entriesStart = entriesPtr + 0x20;
                
                for (int i = 0; i < arrayLength && i < 300; i++)
                {
                    try
                    {
                        var entryAddr = entriesStart + (uint)(i * entrySize);
                        
                        // Check if entry is valid (hashCode >= 0 means it's used)
                        var hashCode = Memory.ReadValue<int>(entryAddr + hashCodeOffset);
                        if (hashCode < 0)
                            continue; // Empty slot
                        
                        // Read MongoID key
                        var mongoId = Memory.ReadValue<MongoID>(entryAddr + keyOffset);
                        var conditionId = mongoId.ReadString();
                        
                        if (string.IsNullOrEmpty(conditionId))
                            continue;
                        
                        // Read TaskConditionCounter pointer
                        var counterPtr = Memory.ReadPtr(entryAddr + valueOffset);
                        if (counterPtr == 0)
                            continue;
                        
                        // Read Value (int at offset 0x40 in TaskConditionCounter)
                        var value = Memory.ReadValue<int>(counterPtr + Offsets.TaskConditionCounter.Value);
                        
                        if (value >= 0 && value < 10000) // Sanity check
                        {
                            _conditionCounters[conditionId] = value;
                            foundCounters++;
                        }
                    }
                    catch
                    {
                        // Skip invalid entries
                    }
                }
                
                if (foundCounters > 0)
                {
                    DebugLogger.LogDebug($"[QuestManager] Found {foundCounters} condition counters");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[QuestManager] Error reading condition counters: {ex.Message}");
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
