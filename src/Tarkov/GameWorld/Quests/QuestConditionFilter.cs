/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using Collections.Pooled;
using System.Collections.Frozen;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Quests
{
    /// <summary>
    /// Handles filtering of quest conditions based on type and completion status.
    /// Extracted from QuestManager to separate filtering logic.
    /// </summary>
    internal static class QuestConditionFilter
    {
        /// <summary>
        /// Objective types that should be skipped during filtering.
        /// </summary>
        private static readonly FrozenSet<QuestObjectiveType> _skipObjectiveTypes = new HashSet<QuestObjectiveType>
        {
            QuestObjectiveType.BuildWeapon,
            QuestObjectiveType.GiveQuestItem,
            QuestObjectiveType.Extract,
            QuestObjectiveType.Shoot,
            QuestObjectiveType.TraderLevel,
            QuestObjectiveType.GiveItem
        }.ToFrozenSet();

        /// <summary>
        /// Map Identifier of Current Map.
        /// </summary>
        private static string MapID => Memory.MapID ?? QuestConstants.DefaultMapId;

        /// <summary>
        /// Filter conditions from a task and populate item/location collections.
        /// </summary>
        public static void FilterConditions(
            TarkovDataManager.TaskElement task,
            string questId,
            PooledSet<string> completedConditions,
            PooledSet<string> masterItems,
            PooledSet<string> masterLocations,
            ConcurrentDictionary<string, byte> itemsDict,
            ConcurrentDictionary<string, QuestLocation> locationsDict)
        {
            if (task?.Objectives is null)
                return;

            foreach (var objective in task.Objectives)
            {
                try
                {
                    if (objective is null)
                        continue;

                    // Skip if objective is completed
                    if (!string.IsNullOrEmpty(objective.Id) && completedConditions.Contains(objective.Id))
                        continue;

                    if (_skipObjectiveTypes.Contains(objective.Type))
                        continue;

                    ProcessObjective(objective, questId, masterItems, masterLocations, itemsDict, locationsDict, completedConditions);
                }
                catch
                {
                    // Skip invalid objectives
                }
            }
        }

        private static void ProcessObjective(
            TarkovDataManager.TaskElement.ObjectiveElement objective,
            string questId,
            PooledSet<string> masterItems,
            PooledSet<string> masterLocations,
            ConcurrentDictionary<string, byte> itemsDict,
            ConcurrentDictionary<string, QuestLocation> locationsDict,
            PooledSet<string> completedConditions)
        {
            // Handle quest items
            if (objective.Type == QuestObjectiveType.FindQuestItem)
            {
                if (objective.QuestItem?.Id is not null)
                {
                    masterItems.Add(objective.QuestItem.Id);
                    _ = itemsDict.GetOrAdd(objective.QuestItem.Id, 0);
                }
                return;
            }

            // Handle regular items
            if (objective.Type == QuestObjectiveType.FindItem)
            {
                if (objective.Item?.Id is not null)
                {
                    masterItems.Add(objective.Item.Id);
                    _ = itemsDict.GetOrAdd(objective.Item.Id, 0);
                }
            }

            // Handle location-based objectives
            if (IsLocationObjective(objective.Type))
            {
                ProcessLocationObjective(objective, questId, masterLocations, locationsDict, completedConditions);
            }
        }

        private static bool IsLocationObjective(QuestObjectiveType type)
        {
            return type == QuestObjectiveType.Visit ||
                   type == QuestObjectiveType.Mark ||
                   type == QuestObjectiveType.PlantItem ||
                   type == QuestObjectiveType.PlantQuestItem;
        }

        private static void ProcessLocationObjective(
            TarkovDataManager.TaskElement.ObjectiveElement objective,
            string questId,
            PooledSet<string> masterLocations,
            ConcurrentDictionary<string, QuestLocation> locationsDict,
            PooledSet<string> completedConditions)
        {
            if (objective.Zones is null || objective.Zones.Count == 0)
                return;

            // Skip if objective is completed
            if (!string.IsNullOrEmpty(objective.Id) && completedConditions.Contains(objective.Id))
                return;

            if (!TarkovDataManager.TaskZones.TryGetValue(MapID, out var zonesForMap))
                return;

            foreach (var zone in objective.Zones)
            {
                if (zone?.Id is string zoneId && zonesForMap.TryGetValue(zoneId, out var pos))
                {
                    var locKey = CreateLocationKey(questId, objective.Id, zoneId);
                    locationsDict.GetOrAdd(locKey, _ => new QuestLocation(questId, objective.Id, pos));
                    masterLocations.Add(locKey);
                }
            }
        }

        /// <summary>
        /// Creates a unique key for a quest location.
        /// </summary>
        public static string CreateLocationKey(string questId, string objectiveId, string zoneId)
        {
            return $"{questId}:{objectiveId}:{zoneId}";
        }
    }
}
