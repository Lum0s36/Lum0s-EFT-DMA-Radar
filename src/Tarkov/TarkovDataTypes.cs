/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Tarkov.GameWorld.Quests;

namespace LoneEftDmaRadar.Tarkov
{
    /// <summary>
    /// Data types used by TarkovDataManager for deserialization.
    /// </summary>
    public static partial class TarkovDataManager
    {
        public sealed class TarkovData
        {
            [JsonPropertyName("items")]
            public List<Web.TarkovDev.Data.TarkovMarketItem> Items { get; set; } = new();

            [JsonPropertyName("maps")]
            public List<MapElement> Maps { get; set; } = new();

            [JsonPropertyName("playerLevels")]
            public List<PlayerLevelElement> PlayerLevels { get; set; }

            [JsonPropertyName("tasks")]
            public List<TaskElement> Tasks { get; set; } = new();
        }

        public class PositionElement
        {
            [JsonPropertyName("x")]
            public float X { get; set; }

            [JsonPropertyName("y")]
            public float Y { get; set; }

            [JsonPropertyName("z")]
            public float Z { get; set; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vector3 AsVector3() => new(X, Y, Z);
        }

        public partial class MapElement
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("nameId")]
            public string NameId { get; set; }

            [JsonPropertyName("extracts")]
            public List<ExtractElement> Extracts { get; set; } = new();

            [JsonPropertyName("transits")]
            public List<TransitElement> Transits { get; set; } = new();

            [JsonPropertyName("hazards")]
            public List<HazardElement> Hazards { get; set; } = new();
        }

        public partial class PlayerLevelElement
        {
            [JsonPropertyName("exp")]
            public int Exp { get; set; }

            [JsonPropertyName("level")]
            public int Level { get; set; }
        }

        public partial class HazardElement
        {
            [JsonPropertyName("hazardType")]
            public string HazardType { get; set; }

            [JsonPropertyName("position")]
            public PositionElement Position { get; set; }
        }

        public partial class ExtractElement
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("faction")]
            public string Faction { get; set; }

            [JsonPropertyName("position")]
            public PositionElement Position { get; set; }

            [JsonIgnore]
            public bool IsPmc => Faction?.Equals("pmc", StringComparison.OrdinalIgnoreCase) ?? false;
            [JsonIgnore]
            public bool IsShared => Faction?.Equals("shared", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        public partial class TransitElement
        {
            [JsonPropertyName("description")]
            public string Description { get; set; }

            [JsonPropertyName("position")]
            public PositionElement Position { get; set; }
        }

        public partial class TaskElement
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("trader")]
            public TaskTraderElement Trader { get; set; }

            [JsonPropertyName("map")]
            public ObjectiveElement.TaskMapElement Map { get; set; }

            [JsonPropertyName("objectives")]
            public List<ObjectiveElement> Objectives { get; set; }

            [JsonPropertyName("neededKeys")]
            public List<NeededKeyGroup> NeededKeys { get; set; }

            [JsonPropertyName("kappaRequired")]
            public bool KappaRequired { get; set; }

            [JsonPropertyName("lightkeeperRequired")]
            public bool LightkeeperRequired { get; set; }

            public class TaskTraderElement
            {
                [JsonPropertyName("name")]
                public string Name { get; set; }
            }

            public class NeededKeyGroup
            {
                [JsonPropertyName("keys")]
                public List<ObjectiveElement.MarkerItemClass> Keys { get; set; }

                [JsonPropertyName("map")]
                public ObjectiveElement.TaskMapElement Map { get; set; }
            }

            public partial class ObjectiveElement
            {
                [JsonPropertyName("id")]
                public string Id { get; set; }

                [JsonPropertyName("type")]
#pragma warning disable IDE1006
                public string _type { get; set; }
#pragma warning restore IDE1006

                [JsonIgnore]
                public QuestObjectiveType Type => _type switch
                {
                    "visit" => QuestObjectiveType.Visit,
                    "mark" => QuestObjectiveType.Mark,
                    "giveItem" => QuestObjectiveType.GiveItem,
                    "shoot" => QuestObjectiveType.Shoot,
                    "extract" => QuestObjectiveType.Extract,
                    "findQuestItem" => QuestObjectiveType.FindQuestItem,
                    "giveQuestItem" => QuestObjectiveType.GiveQuestItem,
                    "findItem" => QuestObjectiveType.FindItem,
                    "buildWeapon" => QuestObjectiveType.BuildWeapon,
                    "plantItem" => QuestObjectiveType.PlantItem,
                    "plantQuestItem" => QuestObjectiveType.PlantQuestItem,
                    "traderLevel" => QuestObjectiveType.TraderLevel,
                    "traderStanding" => QuestObjectiveType.TraderStanding,
                    "skill" => QuestObjectiveType.Skill,
                    "experience" => QuestObjectiveType.Experience,
                    "useItem" => QuestObjectiveType.UseItem,
                    "sellItem" => QuestObjectiveType.SellItem,
                    "taskStatus" => QuestObjectiveType.TaskStatus,
                    _ => QuestObjectiveType.Unknown
                };

                [JsonPropertyName("description")]
                public string Description { get; set; }

                [JsonPropertyName("requiredKeys")]
                public List<List<MarkerItemClass>> RequiredKeys { get; set; }

                [JsonPropertyName("maps")]
                public List<TaskMapElement> Maps { get; set; }

                [JsonPropertyName("zones")]
                public List<TaskZoneElement> Zones { get; set; }

                [JsonPropertyName("count")]
                public int Count { get; set; }

                [JsonPropertyName("foundInRaid")]
                public bool FoundInRaid { get; set; }

                [JsonPropertyName("item")]
                public MarkerItemClass Item { get; set; }

                [JsonPropertyName("questItem")]
                public ObjectiveQuestItem QuestItem { get; set; }

                [JsonPropertyName("markerItem")]
                public MarkerItemClass MarkerItem { get; set; }

                public class MarkerItemClass
                {
                    [JsonPropertyName("id")]
                    public string Id { get; set; }

                    [JsonPropertyName("name")]
                    public string Name { get; set; }

                    [JsonPropertyName("shortName")]
                    public string ShortName { get; set; }
                }

                public class ObjectiveQuestItem
                {
                    [JsonPropertyName("id")]
                    public string Id { get; set; }

                    [JsonPropertyName("name")]
                    public string Name { get; set; }

                    [JsonPropertyName("shortName")]
                    public string ShortName { get; set; }

                    [JsonPropertyName("normalizedName")]
                    public string NormalizedName { get; set; }

                    [JsonPropertyName("description")]
                    public string Description { get; set; }
                }

                public class TaskZoneElement
                {
                    [JsonPropertyName("id")]
                    public string Id { get; set; }

                    [JsonPropertyName("position")]
                    public PositionElement Position { get; set; }

                    [JsonPropertyName("map")]
                    public TaskMapElement Map { get; set; }
                }

                public class TaskMapElement
                {
                    [JsonPropertyName("nameId")]
                    public string NameId { get; set; }

                    [JsonPropertyName("normalizedName")]
                    public string NormalizedName { get; set; }

                    [JsonPropertyName("name")]
                    public string Name { get; set; }
                }
            }
        }
    }
}
