/*
 * Quest Helper - Quest Data
 * Based on analysis of eft-dma-radar_newUI_archive
 * Adapted for IL2CPP
 */

using SDK;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Quests
{
    /// <summary>
    /// Represents a player's quest status and related data.
    /// </summary>
    public sealed class QuestData
    {
        /// <summary>
        /// Quest template ID (BSG ID).
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Quest name (localized if available).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Current quest status.
        /// </summary>
        public EQuestStatus Status { get; set; }

        /// <summary>
        /// Start timestamp (Unix time).
        /// </summary>
        public int StartTime { get; set; }

        /// <summary>
        /// Available after timestamp (for time-gated quests).
        /// </summary>
        public int AvailableAfter { get; set; }

        /// <summary>
        /// List of required item IDs for this quest.
        /// </summary>
        public List<string> RequiredItemIds { get; } = new();

        /// <summary>
        /// List of zone IDs to visit for this quest.
        /// </summary>
        public List<string> RequiredZoneIds { get; } = new();

        /// <summary>
        /// Set of completed condition IDs for this quest.
        /// </summary>
        public HashSet<string> CompletedConditions { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Memory address of the QuestStatusData object.
        /// </summary>
        public ulong Address { get; }

        /// <summary>
        /// Whether this quest is currently active (started but not finished).
        /// </summary>
        public bool IsActive => Status == EQuestStatus.Started || Status == EQuestStatus.AvailableForFinish;

        public QuestData(ulong address, string id)
        {
            Address = address;
            Id = id;
            Name = id; // Default to ID, will be updated if name is available
            Status = EQuestStatus.Locked;
        }

        /// <summary>
        /// Check if a specific item is required for this quest.
        /// </summary>
        public bool RequiresItem(string itemId)
        {
            return RequiredItemIds.Contains(itemId, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if a specific zone is required for this quest.
        /// </summary>
        public bool RequiresZone(string zoneId)
        {
            return RequiredZoneIds.Contains(zoneId, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if a specific condition/objective is completed.
        /// </summary>
        public bool IsConditionCompleted(string conditionId)
        {
            return CompletedConditions.Contains(conditionId);
        }
    }

    /// <summary>
    /// Quest status enumeration matching EFT.Quests.EQuestStatus
    /// </summary>
    public enum EQuestStatus
    {
        Locked = 0,
        AvailableForStart = 1,
        Started = 2,
        AvailableForFinish = 3,
        Success = 4,
        Fail = 5,
        FailRestartable = 6,
        MarkedAsFailed = 7,
        Expired = 8,
        AvailableAfter = 9,
    }
}
