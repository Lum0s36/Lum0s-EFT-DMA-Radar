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

using System.Collections.Frozen;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.Web.TarkovDev.Data;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Quests
{
    /// <summary>
    /// Static database of all quests and their requirements from tarkov.dev API.
    /// Initialized by TarkovDataManager after loading quest data.
    /// </summary>
    public static class QuestDatabase
    {
        private static readonly Lock _syncLock = new();
        private static bool _initialized;

        /// <summary>
        /// All quests keyed by quest ID.
        /// </summary>
        public static FrozenDictionary<string, QuestInfo> AllQuests { get; private set; } = 
            FrozenDictionary<string, QuestInfo>.Empty;

        /// <summary>
        /// All quest zones keyed by zone ID.
        /// </summary>
        public static FrozenDictionary<string, QuestZoneInfo> AllZones { get; private set; } = 
            FrozenDictionary<string, QuestZoneInfo>.Empty;

        /// <summary>
        /// Map of item ID to list of quest IDs that require this item.
        /// </summary>
        public static FrozenDictionary<string, List<string>> ItemToQuestMap { get; private set; } = 
            FrozenDictionary<string, List<string>>.Empty;

        /// <summary>
        /// Map of zone ID to list of quest IDs that require this zone.
        /// </summary>
        public static FrozenDictionary<string, List<string>> ZoneToQuestMap { get; private set; } = 
            FrozenDictionary<string, List<string>>.Empty;

        /// <summary>
        /// True if the database has been initialized.
        /// </summary>
        public static bool IsInitialized => _initialized;

        /// <summary>
        /// Initialize the quest database from tarkov.dev data.
        /// </summary>
        public static void Initialize(List<TaskElement> tasks)
        {
            if (tasks == null || tasks.Count == 0)
            {
                DebugLogger.LogDebug("[QuestDatabase] No tasks provided for initialization");
                return;
            }

            lock (_syncLock)
            {
                try
                {
                    var quests = new Dictionary<string, QuestInfo>(StringComparer.OrdinalIgnoreCase);
                    var zones = new Dictionary<string, QuestZoneInfo>(StringComparer.OrdinalIgnoreCase);
                    var itemToQuest = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                    var zoneToQuest = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                    foreach (var task in tasks)
                    {
                        if (task == null || string.IsNullOrEmpty(task.Id))
                            continue;

                        var questInfo = new QuestInfo
                        {
                            Id = task.Id,
                            Name = task.Name ?? task.Id,
                            TraderName = task.Trader?.Name ?? "Unknown",
                            MapId = task.Map?.NameId,
                            RequiredItemIds = new List<string>(),
                            RequiredZoneIds = new List<string>(),
                            RequiredKeyIds = new List<string>()
                        };

                        // Process objectives
                        if (task.Objectives != null)
                        {
                            foreach (var obj in task.Objectives)
                            {
                                ProcessObjective(obj, questInfo, zones, zoneToQuest, itemToQuest);
                            }
                        }

                        // Process needed keys
                        if (task.NeededKeys != null)
                        {
                            foreach (var keyReq in task.NeededKeys)
                            {
                                if (keyReq?.Keys != null)
                                {
                                    foreach (var key in keyReq.Keys)
                                    {
                                        if (key != null && !string.IsNullOrEmpty(key.Id))
                                        {
                                            questInfo.RequiredKeyIds.Add(key.Id);
                                            AddToItemMap(itemToQuest, key.Id, task.Id);
                                        }
                                    }
                                }
                            }
                        }

                        quests[task.Id] = questInfo;
                    }

                    AllQuests = quests.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
                    AllZones = zones.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
                    ItemToQuestMap = itemToQuest.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
                    ZoneToQuestMap = zoneToQuest.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

                    _initialized = true;
                    DebugLogger.LogDebug($"[QuestDatabase] Initialized with {quests.Count} quests, {zones.Count} zones");
                }
                catch (Exception ex)
                {
                    DebugLogger.LogDebug($"[QuestDatabase] Initialization failed: {ex}");
                }
            }
        }

        private static void ProcessObjective(
            TaskObjectiveElement obj,
            QuestInfo questInfo,
            Dictionary<string, QuestZoneInfo> zones,
            Dictionary<string, List<string>> zoneToQuest,
            Dictionary<string, List<string>> itemToQuest)
        {
            if (obj == null)
                return;

            // Process required items
            if (obj.Item != null && !string.IsNullOrEmpty(obj.Item.Id))
            {
                questInfo.RequiredItemIds.Add(obj.Item.Id);
                AddToItemMap(itemToQuest, obj.Item.Id, questInfo.Id);
            }

            // Process quest items (special quest-only items)
            if (obj.QuestItem != null && !string.IsNullOrEmpty(obj.QuestItem.Id))
            {
                questInfo.RequiredItemIds.Add(obj.QuestItem.Id);
                AddToItemMap(itemToQuest, obj.QuestItem.Id, questInfo.Id);
            }

            // Process marker items (e.g., MS2000 markers)
            if (obj.MarkerItem != null && !string.IsNullOrEmpty(obj.MarkerItem.Id))
            {
                questInfo.RequiredItemIds.Add(obj.MarkerItem.Id);
                AddToItemMap(itemToQuest, obj.MarkerItem.Id, questInfo.Id);
            }

            // Process zones
            if (obj.Zones != null)
            {
                foreach (var zone in obj.Zones)
                {
                    if (zone == null || string.IsNullOrEmpty(zone.Id))
                        continue;

                    questInfo.RequiredZoneIds.Add(zone.Id);
                    AddToZoneMap(zoneToQuest, zone.Id, questInfo.Id);

                    // Create zone info if not exists
                    if (!zones.ContainsKey(zone.Id))
                    {
                        var zoneInfo = new QuestZoneInfo
                        {
                            Id = zone.Id,
                            Description = obj.Description,
                            MapId = zone.Map?.NameId,
                            ObjectiveType = obj.Type,
                            Position = zone.Position != null 
                                ? new Vector3(zone.Position.X, zone.Position.Y, zone.Position.Z)
                                : Vector3.Zero,
                            QuestIds = new List<string> { questInfo.Id }
                        };
                        zones[zone.Id] = zoneInfo;
                    }
                    else
                    {
                        // Add quest to existing zone
                        if (!zones[zone.Id].QuestIds.Contains(questInfo.Id))
                        {
                            zones[zone.Id].QuestIds.Add(questInfo.Id);
                        }
                    }
                }
            }
        }

        private static void AddToItemMap(Dictionary<string, List<string>> map, string itemId, string questId)
        {
            if (!map.TryGetValue(itemId, out var list))
            {
                list = new List<string>();
                map[itemId] = list;
            }
            if (!list.Contains(questId))
                list.Add(questId);
        }

        private static void AddToZoneMap(Dictionary<string, List<string>> map, string zoneId, string questId)
        {
            if (!map.TryGetValue(zoneId, out var list))
            {
                list = new List<string>();
                map[zoneId] = list;
            }
            if (!list.Contains(questId))
                list.Add(questId);
        }

        /// <summary>
        /// Get the quest name for a quest ID.
        /// </summary>
        public static string GetQuestName(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return null;
            return AllQuests.TryGetValue(questId, out var quest) ? quest.Name : null;
        }

        /// <summary>
        /// Get quests that require a specific item.
        /// </summary>
        public static IEnumerable<QuestInfo> GetQuestsForItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                yield break;

            if (ItemToQuestMap.TryGetValue(itemId, out var questIds))
            {
                foreach (var questId in questIds)
                {
                    if (AllQuests.TryGetValue(questId, out var quest))
                        yield return quest;
                }
            }
        }

        /// <summary>
        /// Get quests that require a specific zone.
        /// </summary>
        public static IEnumerable<QuestInfo> GetQuestsForZone(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId))
                yield break;

            if (ZoneToQuestMap.TryGetValue(zoneId, out var questIds))
            {
                foreach (var questId in questIds)
                {
                    if (AllQuests.TryGetValue(questId, out var quest))
                        yield return quest;
                }
            }
        }

        /// <summary>
        /// Get zone info by zone ID.
        /// </summary>
        public static QuestZoneInfo GetZoneInfo(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId))
                return null;
            return AllZones.TryGetValue(zoneId, out var zone) ? zone : null;
        }

        /// <summary>
        /// Check if an item is required for any quest.
        /// </summary>
        public static bool IsQuestItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return false;
            return ItemToQuestMap.ContainsKey(itemId);
        }

        /// <summary>
        /// Get all zones for a specific map.
        /// </summary>
        public static IEnumerable<QuestZoneInfo> GetZonesForMap(string mapId)
        {
            if (string.IsNullOrEmpty(mapId))
                yield break;

            foreach (var zone in AllZones.Values)
            {
                if (string.Equals(zone.MapId, mapId, StringComparison.OrdinalIgnoreCase))
                    yield return zone;
            }
        }
    }

    /// <summary>
    /// Information about a quest from tarkov.dev.
    /// </summary>
    public class QuestInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string TraderName { get; set; }
        public string MapId { get; set; }
        public List<string> RequiredItemIds { get; set; }
        public List<string> RequiredZoneIds { get; set; }
        public List<string> RequiredKeyIds { get; set; }
    }

    /// <summary>
    /// Information about a quest zone from tarkov.dev.
    /// </summary>
    public class QuestZoneInfo
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string MapId { get; set; }
        public string ObjectiveType { get; set; }
        public Vector3 Position { get; set; }
        public List<string> QuestIds { get; set; }
    }
}
