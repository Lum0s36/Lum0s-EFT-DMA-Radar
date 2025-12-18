/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * 
 * Exfil Status Tracking: Credit to Keegi
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

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.Unity;
using LoneEftDmaRadar.UI.Radar.Maps;
using LoneEftDmaRadar.UI.Skia;
using SDK;
using System.Collections.Frozen;
using System.ComponentModel;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Exits
{
    public class Exfil : IExitPoint, IWorldEntity, IMapEntity, IMouseoverEntity
    {
        /// <summary>
        /// Creates an Exfil from static map data (fallback when no memory data available).
        /// </summary>
        public Exfil(TarkovDataManager.ExtractElement extract)
        {
            Name = extract.Name;
            _position = extract.Position.AsVector3();
            ExfilBase = 0; // No memory address
        }

        /// <summary>
        /// Creates an Exfil from game memory with live status updates.
        /// </summary>
        public Exfil(ulong exfilAddr, string exfilName, string mapId, bool isPmc, Vector3 position)
        {
            ExfilBase = exfilAddr;
            _position = position;
            
            // Try to get friendly name from the lookup table
            if (ExfilNames.TryGetValue(mapId, out var mapExfils) && 
                mapExfils.TryGetValue(exfilName, out var friendlyName))
            {
                Name = friendlyName;
            }
            else
            {
                Name = exfilName;
            }
        }

        /// <summary>
        /// Memory address of the exfil point (0 if from static data).
        /// </summary>
        public ulong ExfilBase { get; init; }

        public string Name { get; }

        public EStatus Status { get; private set; } = EStatus.Open;

        /// <summary>
        /// Updates the exfil status from game memory.
        /// </summary>
        public void Update(Enums.EExfiltrationStatus status)
        {
            Status = status switch
            {
                Enums.EExfiltrationStatus.NotPresent => EStatus.Closed,
                Enums.EExfiltrationStatus.Hidden => EStatus.Closed,
                Enums.EExfiltrationStatus.UncompleteRequirements => EStatus.Pending,
                Enums.EExfiltrationStatus.Pending => EStatus.Pending,
                Enums.EExfiltrationStatus.AwaitsManualActivation => EStatus.Pending,
                Enums.EExfiltrationStatus.Countdown => EStatus.Open,
                Enums.EExfiltrationStatus.RegularMode => EStatus.Open,
                _ => EStatus.Pending
            };
        }

        #region Interfaces

        private readonly Vector3 _position;
        public ref readonly Vector3 Position => ref _position;
        public Vector2 MouseoverPosition { get; set; }

        public void Draw(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            // Skip drawing if closed and not showing closed exfils
            if (Status == EStatus.Closed)
                return;

            var heightDiff = Position.Y - localPlayer.Position.Y;
            
            var paint = Status switch
            {
                EStatus.Open => SKPaints.PaintExfilOpen,
                EStatus.Pending => SKPaints.PaintExfilPending,
                EStatus.Closed => SKPaints.PaintExfilClosed,
                _ => SKPaints.PaintExfilOpen
            };

            var point = Position.ToMapPos(mapParams.Map).ToZoomedPos(mapParams);
            MouseoverPosition = new Vector2(point.X, point.Y);
            SKPaints.ShapeOutline.StrokeWidth = 2f;
            if (heightDiff > 1.85f) // exfil is above player
            {
                using var path = point.GetUpArrow(6.5f);
                canvas.DrawPath(path, SKPaints.ShapeOutline);
                canvas.DrawPath(path, paint);
            }
            else if (heightDiff < -1.85f) // exfil is below player
            {
                using var path = point.GetDownArrow(6.5f);
                canvas.DrawPath(path, SKPaints.ShapeOutline);
                canvas.DrawPath(path, paint);
            }
            else // exfil is level with player
            {
                float size = 4.75f * App.Config.UI.UIScale;
                canvas.DrawCircle(point, size, SKPaints.ShapeOutline);
                canvas.DrawCircle(point, size, paint);
            }
        }

        public void DrawMouseover(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            var exfilName = Name ?? "unknown";
            var statusText = Status switch
            {
                EStatus.Open => "Open",
                EStatus.Pending => "Pending",
                EStatus.Closed => "Closed",
                _ => "Unknown"
            };
            
            var text = $"{exfilName} ({statusText})";
            Position.ToMapPos(mapParams.Map).ToZoomedPos(mapParams).DrawMouseoverText(canvas, text);
        }

        #endregion

        public enum EStatus
        {
            [Description(nameof(Open))] Open,
            [Description(nameof(Pending))] Pending,
            [Description(nameof(Closed))] Closed
        }

        #region Exfil Name Lookup Tables

        public static FrozenDictionary<string, FrozenDictionary<string, string>> ExfilNames { get; } = new Dictionary<string, FrozenDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "woods", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Factory Gate", "Friendship Bridge (Co-Op)" },
                { "RUAF Gate", "RUAF Gate" },
                { "ZB-016", "ZB-016" },
                { "ZB-014", "ZB-014" },
                { "UN Roadblock", "UN Roadblock" },
                { "South V-Ex", "Bridge V-Ex" },
                { "Outskirts", "Outskirts" },
                { "un-sec", "Northern UN Roadblock" },
                { "wood_sniper_exit", "Power Line Passage (Flare)" },
                { "woods_secret_minefield", "Railway Bridge to Tarkov (Secret)" },
                { "Friendship Bridge (Co-Op)", "Friendship Bridge (Co-Op)" },
                { "Outskirts Water", "Scav Bridge" },
                { "Dead Man's Place", "Dead Man's Place" },
                { "The Boat", "Boat" },
                { "Scav House", "Scav House" },
                { "East Gate", "Scav Bunker" },
                { "Mountain Stash", "Mountain Stash" },
                { "West Border", "Eastern Rocks" },
                { "Old Station", "Old Railway Depot" },
                { "RUAF Roadblock", "RUAF Roadblock" },
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase) },
            { "shoreline", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Shorl_V-Ex", "Road to North V-Ex" },
                { "Road to Customs", "Road to Customs" },
                { "Road_at_railbridge", "Railway Bridge" },
                { "Tunnel", "Tunnel" },
                { "Lighthouse_pass", "Path to Lighthouse" },
                { "Smugglers_Trail_coop", "Smuggler's Path (Co-op)" },
                { "Pier Boat", "Pier Boat" },
                { "RedRebel_alp", "Climber's Trail" },
                { "shoreline_secret_heartbeat", "Mountain Bunker (Secret)" },
                { "Scav Road to Customs", "Road to Customs" },
                { "Lighthouse", "Lighthouse" },
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase) },
            { "bigmap", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "EXFIL_ZB013", "ZB-013" },
                { "EXFIL_FACTORYSHACKS", "Factory Shacks" },
                { "EXFIL_ZB012", "ZB-012" },
                { "EXFIL_TRAIN", "Railroad to Tarkov V-Ex" },
                { "EXFIL_SMUGGLERS", "Smuggler's Boat" },
                { "EXFIL_RUAFGATE", "RUAF Roadblock" },
                { "EXFIL_OLDROAD", "Old Road Gate" },
                { "EXFIL_CROSSROADS", "Crossroads" },
                { "EXFIL_DORMS", "Dorms V-Ex" },
                { "EXFIL_SCAVLANDS", "Scav Lands" },
                { "beyond_fuel_tank", "Beyond Fuel Tank" },
                { "EXFIL_MILBASE_COOP", "Factory Far Corner" },
                { "customs_secret_siren_base", "Scav CP Basement (Secret)" },
                { "Warehouse 17", "Warehouse 17" },
                { "Old Gas Station", "Old Gas Station" },
                { "Railroad to Tarkov", "Railroad to Tarkov" },
                { "SCAV Lands", "SCAV Lands" },
                { "Passage Between Rocks", "Passage Between Rocks" },
                { "Hole in the Fence", "Hole in the Fence" },
                { "Railroad To Port", "Railroad To Port" },
                { "Factory Far Corner", "Factory Far Corner" },
                { "Railroad to Military Base", "Railroad to Military Base" },
                { "Office Window", "Office Window" },
                { "Sniper Roadblock", "Sniper Roadblock" },
                { "Crossroads", "Crossroads" },
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase) },
            { "interchange", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "EXFIL_EMERCOM", "Emercom Checkpoint" },
                { "EXFIL_PP", "Hole in Fence" },
                { "EXFIL_SAFEHOUSE", "Safe Room (Co-Op)" },
                { "EXFIL_INTERCHANGE_VEXIT_COOP", "Power Station V-Ex" },
                { "interchange_secret_atrium", "Collapsed Atrium (Secret)" },
                { "NW Exfil", "Railway Exfil" },
                { "SE Exfil", "Emercom Checkpoint" },
                { "SafeRoom Exfil", "Safe Room (Co-Op)" },
                { "Scav Camp", "Scav Camp" },
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase) },
            { "rezervbase", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "EXFIL_Train", "Armored Train" },
                { "EXFIL_Bunker_D2", "D-2 Bunker" },
                { "EXFIL_Hermetic", "Sewer Manhole" },
                { "EXFIL_Mountain_Pass", "Cliff Descent" },
                { "EXFIL_PP_EXFIL", "Scav Lands (Co-Op)" },
                { "EXFIL_BUNKER_COOP", "Bunker Ventilation (Co-Op)" },
                { "rezervbase_secret_elevator_backroom", "RB-RS Elevator (Secret)" },
                { "Sewer Manhole", "Sewer Manhole" },
                { "Hole In Wall", "Hole In Wall" },
                { "D-2 Bunker", "D-2 Bunker" },
                { "Scav Lands", "Scav Lands (Co-Op)" },
                { "Depot Hermetic Door", "Depot Hermetic Door" },
                { "Heating Pipe", "Heating Pipe" },
                { "CP Fence", "Checkpoint Fence" },
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase) },
            { "laboratory", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "lab_Hangar_Gate", "Cargo Elevator" },
                { "lab_Parking_Gate", "Parking Gate" },
                { "lab_Under_Storage_Collector", "Sewage Conduit" },
                { "lab_Elevator_Main_Parking", "Main Elevator" },
                { "lab_Elevator_Cargo", "Freight Elevator" },
                { "lab_Vent", "Ventilation Shaft" },
                { "lab_Elevator_Med", "Medical Elevator" },
                { "lab_Elevator_Tech", "Technical Elevator" },
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase) },
            { "factory4_day", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "exit_factory_gate_0", "Gate 0" },
                { "exit_factory_gate_3", "Gate 3" },
                { "exit_factory_cameraRoom", "Security Room (Co-Op)" },
                { "exit_factory_doctor", "Emergency Exit (Behind Trucks)" },
                { "exit_factory_sniper", "Evacuation Point" },
                { "factory4_secret_power_line", "Tractor at Power Line (Secret)" },
                { "Gate 0", "Gate 0" },
                { "Gate 3", "Gate 3" },
                { "Gate m", "Cellars" },
                { "Office Window", "Office Window (Key)" },
                { "Camera Bunker Door", "Security Room (Co-Op)" },
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase) },
            { "factory4_night", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "exit_factory_gate_0", "Gate 0" },
                { "exit_factory_gate_3", "Gate 3" },
                { "exit_factory_cameraRoom", "Security Room (Co-Op)" },
                { "exit_factory_doctor", "Emergency Exit (Behind Trucks)" },
                { "exit_factory_sniper", "Evacuation Point" },
                { "factory4_secret_power_line", "Tractor at Power Line (Secret)" },
                { "Gate 0", "Gate 0" },
                { "Gate 3", "Gate 3" },
                { "Gate m", "Cellars" },
                { "Office Window", "Office Window (Key)" },
                { "Camera Bunker Door", "Security Room (Co-Op)" },
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase) },
            { "lighthouse", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "V-Ex_light", "Northern Checkpoint (V-Ex)" },
                { "Tunnel", "Side Tunnel (Co-Op)" },
                { "Alpinist_light", "Mountain Pass" },
                { "Shorl_free", "Path to Shoreline" },
                { "Nothern_Checkpoint", "Northern Checkpoint" },
                { "Coastal_South_Road", "Southern Road" },
                { "EXFIL_Train", "Armored Train" },
                { "lighthouse_secret_minefield", "Passage by the Lake (Secret)" },
                { "Side Tunnel (Co-Op)", "Side Tunnel (Co-Op)" },
                { "Shorl_free_scav", "Path to Shoreline" },
                { "Scav_Coastal_South", "Southern Road" },
                { "Scav_Underboat_Hideout", "Hideout Under the Landing Stage" },
                { "Scav_Hideout_at_the_grotto", "Scav Hideout at the Grotto" },
                { "Scav_Industrial_zone", "Industrial Zone Gates" },
                { "Armored Train", "Armored Train" },
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase) },
            { "tarkovstreets", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "E8_yard", "Courtyard" },
                { "E7_car", "Primorsky Ave Taxi V-Ex" },
                { "E1", "Stylobate Building Elevator" },
                { "E4", "Crash Site" },
                { "E2", "Sewer River" },
                { "E3", "Damaged House" },
                { "E5", "Collapsed Crane" },
                { "E6", "??" },
                { "E9_sniper", "Klimov Street" },
                { "Exit_E10_coop", "Pinewood Basement (Co-Op)" },
                { "E7", "Expo Checkpoint" },
                { "streets_secret_onyx", "Smugglers' Basement (Secret)" },
                { "scav_e1", "Basement Descent" },
                { "scav_e2", "Entrance to Catacombs" },
                { "scav_e3", "Ventilation Shaft" },
                { "scav_e4", "Sewer Manhole" },
                { "scav_e5", "Near Kamchatskaya Arch" },
                { "scav_e7", "Cardinal Apartment Complex Parking" },
                { "scav_e8", "Klimov Shopping Mall Exfil" },
                { "scav_e6", "Pinewood Basement (Co-Op)" },
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase) },
            { "Sandbox", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Sandbox_VExit", "Police Cordon V-Ex" },
                { "Unity_free_exit", "Emercom Checkpoint" },
                { "Scav_coop_exit", "Scav Checkpoint (Co-Op)" },
                { "Nakatani_stairs_free_exit", "Nakatani Basement Stairs" },
                { "Sniper_exit", "Mira Ave" },
                { "groundzero_secret_adaptation", "Tartowers Sales Office (Secret)" },
                { "Scav Checkpoint (Co-Op)", "Scav Checkpoint (Co-Op)" },
                { "Emercom Checkpoint", "Emercom Checkpoint" },
                { "Nakatani Basement Stairs", "Nakatani Basement Stairs" },
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase) },
            { "Sandbox_high", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Sandbox_VExit", "Police Cordon V-Ex" },
                { "Unity_free_exit", "Emercom Checkpoint" },
                { "Scav_coop_exit", "Scav Checkpoint (Co-Op)" },
                { "Nakatani_stairs_free_exit", "Nakatani Basement Stairs" },
                { "Sniper_exit", "Mira Ave" },
                { "groundzero_secret_adaptation", "Tartowers Sales Office (Secret)" },
                { "Scav Checkpoint (Co-Op)", "Scav Checkpoint (Co-Op)" },
                { "Emercom Checkpoint", "Emercom Checkpoint" },
                { "Nakatani Basement Stairs", "Nakatani Basement Stairs" },
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase) },
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        #endregion
    }
}
