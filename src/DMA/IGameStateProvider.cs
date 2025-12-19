/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Tarkov.GameWorld;
using LoneEftDmaRadar.Tarkov.GameWorld.Exits;
using LoneEftDmaRadar.Tarkov.GameWorld.Explosives;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Quests;

namespace LoneEftDmaRadar.DMA
{
    /// <summary>
    /// Interface for accessing game state data.
    /// Abstracts the static Memory class to enable testability and reduce coupling.
    /// </summary>
    public interface IGameStateProvider
    {
        /// <summary>
        /// True if currently in a raid.
        /// </summary>
        bool InRaid { get; }

        /// <summary>
        /// The local player (radar user).
        /// </summary>
        LocalPlayer LocalPlayer { get; }

        /// <summary>
        /// All players in the current raid.
        /// </summary>
        IReadOnlyCollection<AbstractPlayer> Players { get; }

        /// <summary>
        /// All exit points in the current raid.
        /// </summary>
        IReadOnlyCollection<IExitPoint> Exits { get; }

        /// <summary>
        /// All explosives (grenades, tripwires) in the current raid.
        /// </summary>
        IReadOnlyCollection<IExplosiveItem> Explosives { get; }

        /// <summary>
        /// Loot manager for the current raid.
        /// </summary>
        LootManager Loot { get; }

        /// <summary>
        /// Quest manager for the current raid.
        /// </summary>
        QuestManager QuestManager { get; }

        /// <summary>
        /// Current map ID.
        /// </summary>
        string MapID { get; }

        /// <summary>
        /// The current game world instance.
        /// </summary>
        LocalGameWorld Game { get; }
    }
}
