/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Tarkov;

namespace LoneEftDmaRadar.UI.Radar.ViewModels.QuestHelper
{
    /// <summary>
    /// Helper methods for quest filtering and map matching.
    /// </summary>
    public static class QuestFilterHelper
    {
        public static string MapDisplayNameToId(string displayName) => displayName switch
        {
            "Customs" => "bigmap",
            "Factory" => "factory4_day",
            "Ground Zero" => "sandbox",
            "Interchange" => "interchange",
            "Labyrinth" => "labyrinth",
            "Labs" => "laboratory",
            "Lighthouse" => "lighthouse",
            "Reserve" => "rezervbase",
            "Shoreline" => "shoreline",
            "Streets" => "tarkovstreets",
            "Woods" => "woods",
            _ => displayName?.ToLowerInvariant() ?? ""
        };

        public static string GetMapName(string mapId) => mapId switch
        {
            "factory4_day" => "Factory (Day)",
            "factory4_night" => "Factory (Night)",
            "bigmap" => "Customs",
            "interchange" => "Interchange",
            "woods" => "Woods",
            "shoreline" => "Shoreline",
            "rezervbase" => "Reserve",
            "laboratory" => "Labs",
            "labyrinth" => "Labyrinth",
            "lighthouse" => "Lighthouse",
            "tarkovstreets" => "Streets",
            "sandbox" or "sandbox_high" => "Ground Zero",
            _ => mapId ?? "Unknown"
        };

        public static bool IsKappaQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return false;
            if (!TarkovDataManager.TaskData.TryGetValue(questId, out var task)) return false;
            return task.KappaRequired;
        }

        public static bool IsLightkeeperQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return false;
            if (!TarkovDataManager.TaskData.TryGetValue(questId, out var task)) return false;
            return task.LightkeeperRequired ||
                   (task.Trader?.Name?.Equals("Lightkeeper", StringComparison.OrdinalIgnoreCase) ?? false);
        }

        public static bool HasObjectivesOnMap(string questId, string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return true;
            if (!TarkovDataManager.TaskData.TryGetValue(questId, out var task)) return false;

            if (task.Map?.NameId != null)
                return IsMapMatch(task.Map.NameId, mapId);

            bool hasMapSpecificObjective = false;
            bool hasAnyMapObjective = false;

            if (task.Objectives != null)
            {
                foreach (var obj in task.Objectives)
                {
                    if (obj.Maps != null && obj.Maps.Count > 0)
                    {
                        foreach (var objMap in obj.Maps)
                        {
                            if (objMap?.NameId != null)
                            {
                                hasMapSpecificObjective = true;
                                if (IsMapMatch(objMap.NameId, mapId))
                                    hasAnyMapObjective = true;
                            }
                        }
                    }

                    if (obj.Zones != null)
                    {
                        foreach (var zone in obj.Zones)
                        {
                            if (zone?.Map?.NameId != null)
                            {
                                hasMapSpecificObjective = true;
                                if (IsMapMatch(zone.Map.NameId, mapId))
                                    hasAnyMapObjective = true;
                            }
                        }
                    }
                }
            }

            if (hasMapSpecificObjective)
                return hasAnyMapObjective;

            return true;
        }

        public static bool IsMapMatch(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) return true;
            if (a.Contains("factory", StringComparison.OrdinalIgnoreCase) &&
                b.Contains("factory", StringComparison.OrdinalIgnoreCase)) return true;
            if (a.Contains("sandbox", StringComparison.OrdinalIgnoreCase) &&
                b.Contains("sandbox", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
