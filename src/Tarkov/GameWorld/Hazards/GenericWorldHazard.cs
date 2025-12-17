/*
 * Lone EFT DMA Radar
 * Hazards System - Generic World Hazard Implementation
*/

using LoneEftDmaRadar.Misc;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.Unity;
using LoneEftDmaRadar.UI.Radar.Maps;
using LoneEftDmaRadar.UI.Skia;
using SkiaSharp;
using System.Text.Json.Serialization;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Hazards
{
    /// <summary>
    /// Generic implementation of a world hazard loaded from TarkovDev data.
    /// </summary>
    public class GenericWorldHazard : IWorldHazard
    {
        private Vector3 _position;

        /// <summary>
        /// Type/description of the hazard.
        /// </summary>
        [JsonPropertyName("hazardType")]
        public string HazardType { get; set; }

        /// <summary>
        /// Position of the hazard in world coordinates.
        /// </summary>
        [JsonPropertyName("position")]
        public Vector3 Position 
        { 
            get => _position; 
            set => _position = value; 
        }

        /// <summary>
        /// Cached position for mouseover detection.
        /// </summary>
        [JsonIgnore]
        public Vector2 MouseoverPosition { get; set; }

        /// <summary>
        /// IWorldEntity Position implementation.
        /// </summary>
        [JsonIgnore]
        ref readonly Vector3 IWorldEntity.Position => ref _position;

        /// <summary>
        /// Draws the hazard marker on the radar.
        /// </summary>
        public void Draw(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            if (!App.Config.UI.ShowHazards)
                return;

            var hazardZoomedPos = Position.ToMapPos(mapParams.Map).ToZoomedPos(mapParams);
            MouseoverPosition = hazardZoomedPos.AsVector2();
            hazardZoomedPos.DrawHazardMarker(canvas);
        }

        /// <summary>
        /// Draws the hazard info when moused over.
        /// </summary>
        public void DrawMouseover(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            Position.ToMapPos(mapParams.Map).ToZoomedPos(mapParams)
                .DrawMouseoverText(canvas, $"Hazard: {HazardType ?? "Unknown"}");
        }
    }
}
