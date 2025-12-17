/*
 * Lone EFT DMA Radar
 * Hazards System - World Hazard Interface
*/

using LoneEftDmaRadar.Tarkov.Unity;
using LoneEftDmaRadar.UI.Skia;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Hazards
{
    /// <summary>
    /// Defines an interface for in-game world hazards (minefields, radiation zones, etc.).
    /// </summary>
    public interface IWorldHazard : IWorldEntity, IMouseoverEntity
    {
        /// <summary>
        /// Description of the hazard/type (e.g., "Minefield", "Radiation", "Sniper").
        /// </summary>
        string HazardType { get; }
    }
}
