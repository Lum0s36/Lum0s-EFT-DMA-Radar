/*
 * Quest Helper - Quest Zone
 * Based on analysis of eft-dma-radar_newUI_archive
 * Adapted for IL2CPP
 */

using LoneEftDmaRadar.Tarkov.Unity.Structures;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Quests
{
    /// <summary>
    /// Represents a Quest Zone (PlaceItemTrigger, QuestTrigger, etc.)
    /// </summary>
    public sealed class QuestZone
    {
        /// <summary>
        /// Zone ID (matches quest condition zoneId).
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Zone description/name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// World position of the zone center.
        /// </summary>
        public Vector3 Position { get; private set; }

        /// <summary>
        /// Zone type (PlaceItem, Quest, etc.)
        /// </summary>
        public QuestZoneType ZoneType { get; }

        /// <summary>
        /// Whether this zone is active for a current quest.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Associated Quest ID (if any).
        /// </summary>
        public string QuestId { get; set; }

        /// <summary>
        /// Memory address of the zone object (can be updated when found in memory).
        /// </summary>
        public ulong Address { get; set; }

        public QuestZone(ulong address, string id, Vector3 position, QuestZoneType type)
        {
            Address = address;
            Id = id;
            Name = id; // Default to ID, will be updated if name is available
            Position = position;
            ZoneType = type;
            IsActive = false;
        }

        /// <summary>
        /// Update the position from memory (in case zone moves).
        /// </summary>
        public void UpdatePosition(Vector3 newPosition)
        {
            Position = newPosition;
        }
    }

    /// <summary>
    /// Types of quest zones.
    /// </summary>
    public enum QuestZoneType
    {
        /// <summary>PlaceItemTrigger - Place item at location</summary>
        PlaceItem,
        /// <summary>QuestTrigger - Visit location</summary>
        Visit,
        /// <summary>Generic trigger zone</summary>
        Generic
    }
}
