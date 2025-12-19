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
    /// Default implementation of IGameStateProvider that delegates to the global Memory singleton.
    /// </summary>
    public sealed class MemoryGameStateProvider : IGameStateProvider
    {
        /// <summary>
        /// Singleton instance (for backward compatibility during migration).
        /// </summary>
        public static MemoryGameStateProvider Instance { get; } = new();

        private MemoryGameStateProvider() { }

        /// <inheritdoc/>
        public bool InRaid => Memory.InRaid;

        /// <inheritdoc/>
        public LocalPlayer LocalPlayer => Memory.LocalPlayer;

        /// <inheritdoc/>
        public IReadOnlyCollection<AbstractPlayer> Players => Memory.Players;

        /// <inheritdoc/>
        public IReadOnlyCollection<IExitPoint> Exits => Memory.Exits;

        /// <inheritdoc/>
        public IReadOnlyCollection<IExplosiveItem> Explosives => Memory.Explosives;

        /// <inheritdoc/>
        public LootManager Loot => Memory.Game?.Loot;

        /// <inheritdoc/>
        public QuestManager QuestManager => Memory.Game?.QuestManager;

        /// <inheritdoc/>
        public string MapID => Memory.MapID;

        /// <inheritdoc/>
        public LocalGameWorld Game => Memory.Game;
    }
}
