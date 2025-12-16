/*
 * Quest Helper - Quest Manager
 * Based on analysis of eft-dma-radar_newUI_archive
 * Adapted for IL2CPP
 * 
 * Reads player's active quests and quest zones from game memory.
 */

using System.Collections.Concurrent;
using LoneEftDmaRadar.Tarkov.Unity;
using LoneEftDmaRadar.Tarkov.Unity.Collections;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Misc;
using SDK;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Quests
{
    /// <summary>
    /// Manages quest data including active quests and quest zones.
    /// </summary>
    public sealed class QuestManager
    {
        #region Fields/Properties

        private readonly ulong _localGameWorld;
        private readonly ConcurrentDictionary<string, QuestData> _quests = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, QuestZone> _zones = new(StringComparer.OrdinalIgnoreCase);
        private readonly Lock _syncLock = new();
        private DateTime _lastZoneScan = DateTime.MinValue;
        private static readonly TimeSpan ZoneScanInterval = TimeSpan.FromSeconds(10);

        /// <summary>
        /// All player quests (keyed by quest ID).
        /// </summary>
        public IReadOnlyDictionary<string, QuestData> Quests => _quests;

        /// <summary>
        /// All quest zones found in the world.
        /// </summary>
        public IReadOnlyDictionary<string, QuestZone> Zones => _zones;

        /// <summary>
        /// Active quests (Started or AvailableForFinish).
        /// </summary>
        public IEnumerable<QuestData> ActiveQuests => _quests.Values.Where(q => q.IsActive);

        /// <summary>
        /// Active zones (zones linked to active quests).
        /// </summary>
        public IEnumerable<QuestZone> ActiveZones => _zones.Values.Where(z => z.IsActive);

        /// <summary>
        /// All item IDs required for active quests.
        /// </summary>
        public IReadOnlySet<string> ActiveQuestItemIds { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// All zone IDs required for active quests.
        /// </summary>
        public IReadOnlySet<string> ActiveQuestZoneIds { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Constructor

        public QuestManager(ulong localGameWorld)
        {
            _localGameWorld = localGameWorld;
            
            // Pre-populate zones from QuestDatabase if available
            if (QuestDatabase.IsInitialized)
            {
                var mapId = Memory.MapID;
                if (!string.IsNullOrEmpty(mapId))
                {
                    foreach (var zoneInfo in QuestDatabase.GetZonesForMap(mapId))
                    {
                        if (zoneInfo.Position != Vector3.Zero)
                        {
                            var zone = new QuestZone(0, zoneInfo.Id, zoneInfo.Position, QuestZoneType.Generic)
                            {
                                Name = zoneInfo.Description ?? zoneInfo.Id,
                                IsActive = false // Will be updated when quests are refreshed
                            };
                            _zones[zoneInfo.Id] = zone;
                        }
                    }
                    DebugLogger.LogDebug($"[QuestManager] Pre-loaded {_zones.Count} zones for map {mapId}");
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Refresh quest data from memory. Call from slow worker thread.
        /// </summary>
        public void Refresh(ulong profilePtr, CancellationToken ct)
        {
            if (!App.Config.QuestHelper.Enabled)
                return;

            try
            {
                ct.ThrowIfCancellationRequested();
                RefreshPlayerQuests(profilePtr, ct);
                
                // Only scan for zones periodically (they don't change often)
                if (DateTime.UtcNow - _lastZoneScan > ZoneScanInterval)
                {
                    RefreshQuestZones(ct);
                    _lastZoneScan = DateTime.UtcNow;
                }
                
                UpdateActiveItems();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[QuestManager] Refresh error: {ex}");
            }
        }

        /// <summary>
        /// Check if an item ID is required for an active quest.
        /// </summary>
        public bool IsQuestItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return false;

            return ActiveQuestItemIds.Contains(itemId);
        }

        /// <summary>
        /// Check if a zone ID is required for an active quest.
        /// </summary>
        public bool IsActiveZone(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId))
                return false;

            return ActiveQuestZoneIds.Contains(zoneId);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Read player's quests from Profile.QuestsData.
        /// </summary>
        private void RefreshPlayerQuests(ulong profilePtr, CancellationToken ct)
        {
            if (profilePtr == 0)
            {
                DebugLogger.LogDebug("[QuestManager] Profile pointer is 0, skipping quest refresh");
                return;
            }

            try
            {
                // Profile.QuestsData is a List<QuestStatusData>
                var questsDataPtr = Memory.ReadPtr(profilePtr + Offsets.Profile.QuestsData);
                if (questsDataPtr == 0)
                {
                    DebugLogger.LogDebug("[QuestManager] QuestsData pointer is 0");
                    return;
                }

                // Read the list
                using var questsList = UnityList<ulong>.Create(addr: questsDataPtr, useCache: false); // Don't cache for fresh data
                
                int processedCount = 0;
                int activeCount = 0;

                foreach (var questDataAddr in questsList)
                {
                    ct.ThrowIfCancellationRequested();

                    if (questDataAddr == 0)
                        continue;

                    try
                    {
                        // Read status first - only process Started quests (status == 2)
                        var statusInt = Memory.ReadValue<int>(questDataAddr + Offsets.QuestStatusData.Status);
                        var status = (EQuestStatus)statusInt;
                        
                        // Only process Started or AvailableForFinish quests
                        if (status != EQuestStatus.Started && status != EQuestStatus.AvailableForFinish)
                            continue;

                        // Read quest ID
                        var idPtr = Memory.ReadPtr(questDataAddr + Offsets.QuestStatusData.Id);
                        if (idPtr == 0)
                            continue;

                        var questId = Memory.ReadUnicodeString(idPtr, 64, false);
                        if (string.IsNullOrEmpty(questId))
                            continue;

                        processedCount++;

                        // Get or create quest data
                        if (!_quests.TryGetValue(questId, out var quest))
                        {
                            quest = new QuestData(questDataAddr, questId);
                            
                            // Try to get quest info from database
                            if (QuestDatabase.IsInitialized && QuestDatabase.AllQuests.TryGetValue(questId, out var questInfo))
                            {
                                quest.Name = questInfo.Name;
                                quest.RequiredItemIds.AddRange(questInfo.RequiredItemIds);
                                quest.RequiredZoneIds.AddRange(questInfo.RequiredZoneIds);
                            }
                            
                            _quests[questId] = quest;
                        }

                        quest.Status = status;
                        quest.StartTime = Memory.ReadValue<int>(questDataAddr + Offsets.QuestStatusData.StartTime);
                        quest.AvailableAfter = Memory.ReadValue<int>(questDataAddr + Offsets.QuestStatusData.AvailableAfter);

                        if (quest.IsActive)
                            activeCount++;

                        // Read CompletedConditions (HashSet<MongoID>)
                        ReadCompletedConditions(questDataAddr, quest);
                    }
                    catch (Exception ex)
                    {
                        // Skip invalid quest data
                        DebugLogger.LogDebug($"[QuestManager] Error reading quest at 0x{questDataAddr:X}: {ex.Message}");
                    }
                }
                
                if (processedCount > 0 || activeCount > 0)
                {
                    DebugLogger.LogDebug($"[QuestManager] Processed {processedCount} quests, {activeCount} active");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[QuestManager] RefreshPlayerQuests error: {ex}");
            }
        }

        /// <summary>
        /// Read CompletedConditions HashSet from QuestStatusData.
        /// </summary>
        private static void ReadCompletedConditions(ulong questDataAddr, QuestData quest)
        {
            try
            {
                var completedConditionsPtr = Memory.ReadPtr(questDataAddr + Offsets.QuestStatusData.CompletedConditions);
                if (completedConditionsPtr == 0)
                {
                    return;
                }

                // Read count first to check if there are any completed conditions
                var count = Memory.ReadValue<int>(completedConditionsPtr + UnityHashSet<MongoID>.CountOffset, false);
                if (count <= 0)
                {
                    return;
                }
                
                DebugLogger.LogDebug($"[QuestManager] Quest {quest.Id}: Found {count} completed conditions in HashSet");

                // Clear existing and re-read
                quest.CompletedConditions.Clear();

                using var completedHashSet = UnityHashSet<MongoID>.Create(completedConditionsPtr, false); // Don't cache for fresh data
                int conditionCount = 0;
                foreach (var entry in completedHashSet)
                {
                    try
                    {
                        var conditionId = entry.Value.ReadString(64, false); // Don't cache
                        if (!string.IsNullOrEmpty(conditionId))
                        {
                            quest.CompletedConditions.Add(conditionId);
                            conditionCount++;
                            DebugLogger.LogDebug($"[QuestManager] Quest {quest.Id}: Completed condition: {conditionId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogDebug($"[QuestManager] Error reading condition entry: {ex.Message}");
                    }
                }
                
                if (conditionCount > 0)
                {
                    DebugLogger.LogDebug($"[QuestManager] Quest {quest.Id}: Successfully read {conditionCount} completed conditions");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[QuestManager] ReadCompletedConditions error for quest {quest.Id}: {ex.Message}");
            }
        }

        /// <summary>
        /// Scan for quest zones in the game world (TriggerWithId objects).
        /// This scans both the loot list and synchronizable objects.
        /// </summary>
        private void RefreshQuestZones(CancellationToken ct)
        {
            try
            {
                // Scan loot list for PlaceItemTrigger and QuestTrigger objects
                ScanLootListForZones(ct);
                
                // Also scan synchronizable objects for triggers
                ScanSynchronizableObjectsForZones(ct);
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[QuestManager] RefreshQuestZones error: {ex}");
            }
        }

        /// <summary>
        /// Scan the loot list for quest zone triggers.
        /// </summary>
        private void ScanLootListForZones(CancellationToken ct)
        {
            try
            {
                var lootListAddr = Memory.ReadPtr(_localGameWorld + Offsets.GameWorld.LootList);
                if (lootListAddr == 0)
                    return;

                using var lootList = UnityList<ulong>.Create(addr: lootListAddr, useCache: true);

                foreach (var objectAddr in lootList)
                {
                    ct.ThrowIfCancellationRequested();

                    if (objectAddr == 0)
                        continue;

                    try
                    {
                        ProcessPotentialQuestZone(objectAddr);
                    }
                    catch
                    {
                        // Skip invalid objects
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[QuestManager] ScanLootListForZones error: {ex}");
            }
        }

        /// <summary>
        /// Scan synchronizable objects for quest zone triggers.
        /// </summary>
        private void ScanSynchronizableObjectsForZones(CancellationToken ct)
        {
            try
            {
                var syncLogicProcessor = Memory.ReadPtr(_localGameWorld + Offsets.GameWorld.SynchronizableObjectLogicProcessor);
                if (syncLogicProcessor == 0)
                    return;

                var syncObjectsListAddr = Memory.ReadPtr(syncLogicProcessor + Offsets.SynchronizableObjectLogicProcessor._staticSynchronizableObjects);
                if (syncObjectsListAddr == 0)
                    return;

                using var syncObjectsList = UnityList<ulong>.Create(addr: syncObjectsListAddr, useCache: true);

                foreach (var objectAddr in syncObjectsList)
                {
                    ct.ThrowIfCancellationRequested();

                    if (objectAddr == 0)
                        continue;

                    try
                    {
                        ProcessPotentialQuestZone(objectAddr);
                    }
                    catch
                    {
                        // Skip invalid objects
                    }
                }
            }
            catch
            {
                // SynchronizableObjects may not exist on all maps
            }
        }

        /// <summary>
        /// Process a potential quest zone object.
        /// </summary>
        private void ProcessPotentialQuestZone(ulong objectAddr)
        {
            // Read the MonoBehaviour/component
            var monoBehaviour = Memory.ReadPtr(objectAddr + ObjectClass.MonoBehaviourOffset);
            if (monoBehaviour == 0)
                return;

            // Get class info to check if this is a quest trigger
            var classPtr = Memory.ReadPtr(monoBehaviour + UnitySDK.UnityOffsets.Component_ObjectClassOffset);
            if (classPtr == 0)
                return;

            var className = ObjectClass.ReadName(classPtr);
            if (string.IsNullOrEmpty(className))
                return;

            // Determine zone type from class name
            QuestZoneType? zoneType = null;
            if (className.Contains("PlaceItemTrigger", StringComparison.OrdinalIgnoreCase))
            {
                zoneType = QuestZoneType.PlaceItem;
            }
            else if (className.Contains("QuestTrigger", StringComparison.OrdinalIgnoreCase) ||
                     className.Contains("ExperienceTrigger", StringComparison.OrdinalIgnoreCase) ||
                     className.Contains("ConditionZoneTrigger", StringComparison.OrdinalIgnoreCase))
            {
                zoneType = QuestZoneType.Visit;
            }
            else if (className.Contains("TriggerWithId", StringComparison.OrdinalIgnoreCase))
            {
                zoneType = QuestZoneType.Generic;
            }

            if (zoneType == null)
                return;

            // Read zone ID from TriggerWithId base
            var idPtr = Memory.ReadPtr(monoBehaviour + Offsets.TriggerWithId.Id);
            if (idPtr == 0)
                return;

            var zoneId = Memory.ReadUnicodeString(idPtr, 64, true);
            if (string.IsNullOrEmpty(zoneId))
                return;

            // Get position from transform
            var position = GetTransformPosition(monoBehaviour);
            if (position == Vector3.Zero)
                return;

            // Check if we already have this zone
            if (_zones.TryGetValue(zoneId, out var existingZone))
            {
                // Update position if we found it in memory (more accurate)
                existingZone.UpdatePosition(position);
                existingZone.Address = monoBehaviour;
                return;
            }

            // Create new zone
            var zone = new QuestZone(monoBehaviour, zoneId, position, zoneType.Value);

            // Try to get description from database first
            if (QuestDatabase.IsInitialized)
            {
                var zoneInfo = QuestDatabase.GetZoneInfo(zoneId);
                if (zoneInfo != null && !string.IsNullOrEmpty(zoneInfo.Description))
                {
                    zone.Name = zoneInfo.Description;
                }
            }

            // Try to read description from memory
            if (string.IsNullOrEmpty(zone.Name) || zone.Name == zoneId)
            {
                try
                {
                    var descPtr = Memory.ReadPtr(monoBehaviour + Offsets.TriggerWithId.Description);
                    if (descPtr != 0)
                    {
                        var desc = Memory.ReadUnicodeString(descPtr, 128, true);
                        if (!string.IsNullOrEmpty(desc))
                        {
                            zone.Name = desc;
                        }
                    }
                }
                catch
                {
                    // Description not available
                }
            }

            _zones[zoneId] = zone;
            DebugLogger.LogDebug($"[QuestManager] Found zone: {zoneId} ({zoneType}) at {position}");
        }

        /// <summary>
        /// Get the world position of a MonoBehaviour's transform.
        /// </summary>
        private static Vector3 GetTransformPosition(ulong monoBehaviour)
        {
            try
            {
                var gameObject = Memory.ReadPtr(monoBehaviour + UnitySDK.UnityOffsets.Component_GameObjectOffset);
                if (gameObject == 0)
                    return Vector3.Zero;

                var components = Memory.ReadPtr(gameObject + UnitySDK.UnityOffsets.GameObject_ComponentsOffset);
                if (components == 0)
                    return Vector3.Zero;

                var transformInternal = Memory.ReadPtr(components + 0x8);
                if (transformInternal == 0)
                    return Vector3.Zero;

                var transform = new UnityTransform(transformInternal, true);
                return transform.UpdatePosition();
            }
            catch
            {
                return Vector3.Zero;
            }
        }

        /// <summary>
        /// Update the set of active quest items and zones.
        /// </summary>
        private void UpdateActiveItems()
        {
            var itemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var zoneIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var quest in ActiveQuests)
            {
                foreach (var itemId in quest.RequiredItemIds)
                {
                    itemIds.Add(itemId);
                }
                foreach (var zoneId in quest.RequiredZoneIds)
                {
                    zoneIds.Add(zoneId);
                }
            }

            ActiveQuestItemIds = itemIds;
            ActiveQuestZoneIds = zoneIds;

            // Update zone active status based on active quests
            foreach (var zone in _zones.Values)
            {
                // Zone is active if it's in the required zones or if we can match it to an active quest
                bool isActive = zoneIds.Contains(zone.Id);
                
                // Also check the QuestDatabase for zone-to-quest mapping
                if (!isActive && QuestDatabase.IsInitialized)
                {
                    var questsForZone = QuestDatabase.GetQuestsForZone(zone.Id);
                    foreach (var questInfo in questsForZone)
                    {
                        if (_quests.TryGetValue(questInfo.Id, out var quest) && quest.IsActive)
                        {
                            isActive = true;
                            break;
                        }
                    }
                }
                
                zone.IsActive = isActive;
            }
        }

        #endregion
    }
}
