/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
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
using LoneEftDmaRadar.Tarkov.Unity.Collections;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Misc;
using SDK;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Exits
{
    /// <summary>
    /// List of PMC/Scav 'Exits' in Local Game World and their position/status.
    /// Reads exfil data from game memory and updates status in real-time.
    /// </summary>
    public sealed class ExitManager : IReadOnlyCollection<IExitPoint>
    {
        private IReadOnlyList<IExitPoint> _exits;

        private readonly ulong _localGameWorld;
        private readonly string _mapId;
        private readonly bool _isPMC;
        private readonly LocalPlayer _localPlayer;

        public ExitManager(ulong localGameWorld, string mapId, LocalPlayer localPlayer)
        {
            _localGameWorld = localGameWorld;
            _mapId = mapId;
            _isPMC = localPlayer?.IsPmc ?? true;
            _localPlayer = localPlayer;
        }

        /// <summary>
        /// Fallback constructor for when no LocalGameWorld is available.
        /// Uses static map data instead of reading from memory.
        /// </summary>
        public ExitManager(string mapId, bool isPMC)
        {
            _localGameWorld = 0;
            _mapId = mapId;
            _isPMC = isPMC;
            _localPlayer = null;
            
            // Use static data fallback
            InitFromStaticData();
        }

        /// <summary>
        /// Initialize exfils from static map data (fallback).
        /// </summary>
        private void InitFromStaticData()
        {
            var list = new List<IExitPoint>();
            if (TarkovDataManager.MapData.TryGetValue(_mapId, out var map))
            {
                var filteredExfils = _isPMC ?
                    map.Extracts.Where(x => x.IsShared || x.IsPmc) :
                    map.Extracts.Where(x => !x.IsPmc);
                foreach (var exfil in filteredExfils)
                {
                    list.Add(new Exfil(exfil));
                }
                foreach (var transit in map.Transits)
                {
                    list.Add(new TransitPoint(transit));
                }
            }
            _exits = list;
        }

        /// <summary>
        /// Initialize exfils from game memory.
        /// </summary>
        private void Init()
        {
            if (_localGameWorld == 0)
            {
                InitFromStaticData();
                return;
            }

            var list = new List<IExitPoint>();

            try
            {
                var exfiltrationController = Memory.ReadPtr(_localGameWorld + Offsets.GameWorld.ExfiltrationController);
                
                // Read PMC or Scav exfil points based on player type
                var exfilArrayAddr = Memory.ReadPtr(exfiltrationController + 
                    (_isPMC ? Offsets.ExfiltrationController.ExfiltrationPoints : Offsets.ExfiltrationController.ScavExfiltrationPoints));
                
                // Also read secret exfils (available to both PMC and Scav)
                var secretExfilArrayAddr = Memory.ReadPtr(exfiltrationController + Offsets.ExfiltrationController.SecretExfiltrationPoints);

                // Process regular exfils
                ProcessExfilArray(exfilArrayAddr, list);
                
                // Process secret exfils
                ProcessExfilArray(secretExfilArrayAddr, list);

                // Add transit points from static data
                if (TarkovDataManager.MapData.TryGetValue(_mapId, out var map))
                {
                    foreach (var transit in map.Transits)
                    {
                        list.Add(new TransitPoint(transit));
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[ExitManager] Init Error: {ex.Message}");
                // Fallback to static data
                InitFromStaticData();
                return;
            }

            _exits = list;
        }

        /// <summary>
        /// Process an array of exfil points from memory.
        /// </summary>
        private void ProcessExfilArray(ulong arrayAddr, List<IExitPoint> list)
        {
            if (!MemDMA.IsValidVirtualAddress(arrayAddr))
                return;

            try
            {
                using var exfilArray = UnityArray<ulong>.Create(arrayAddr, false);
                
                foreach (var exfilAddr in exfilArray)
                {
                    if (!MemDMA.IsValidVirtualAddress(exfilAddr))
                        continue;

                    try
                    {
                        // Read exfil settings
                        var settingsAddr = Memory.ReadPtr(exfilAddr + Offsets.ExfiltrationPoint.Settings, false);
                        if (!MemDMA.IsValidVirtualAddress(settingsAddr))
                            continue;

                        // Read exfil name - Unity string format
                        var namePtr = Memory.ReadPtr(settingsAddr + Offsets.ExitTriggerSettings.Name, false);
                        var exfilName = Memory.ReadUnicodeString(namePtr, 128, false);
                        
                        if (string.IsNullOrEmpty(exfilName))
                            continue;

                        // Read position from static map data based on name
                        var position = GetExfilPositionFromStatic(exfilName);
                        
                        // Check if this exfil is eligible for the player
                        bool isEligible = CheckExfilEligibility(exfilAddr);
                        
                        if (isEligible)
                        {
                            var exfil = new Exfil(exfilAddr, exfilName, _mapId, _isPMC, position);
                            list.Add(exfil);
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogDebug($"[ExitManager] Error processing exfil: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[ExitManager] ProcessExfilArray Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get exfil position from static map data based on name.
        /// </summary>
        private Vector3 GetExfilPositionFromStatic(string exfilName)
        {
            if (!TarkovDataManager.MapData.TryGetValue(_mapId, out var map))
                return Vector3.Zero;

            // Try to find matching extract in static data
            var extract = map.Extracts.FirstOrDefault(e => 
                e.Name.Equals(exfilName, StringComparison.OrdinalIgnoreCase) ||
                (Exfil.ExfilNames.TryGetValue(_mapId, out var names) && 
                 names.TryGetValue(exfilName, out var friendlyName) &&
                 e.Name.Equals(friendlyName, StringComparison.OrdinalIgnoreCase)));

            return extract?.Position?.AsVector3() ?? Vector3.Zero;
        }

        /// <summary>
        /// Check if an exfil is eligible for the current player based on EligibleIds.
        /// </summary>
        private bool CheckExfilEligibility(ulong exfilAddr)
        {
            try
            {
                var eligibleIdsAddr = Memory.ReadPtr(exfilAddr + Offsets.ExfiltrationPoint.EligibleIds, false);
                if (!MemDMA.IsValidVirtualAddress(eligibleIdsAddr))
                    return true; // If no eligibility list, assume eligible
                
                using var eligibleIdsList = UnityList<ulong>.Create(eligibleIdsAddr, false);
                
                // If the list has entries, the exfil is available to the player
                return eligibleIdsList.Count > 0;
            }
            catch
            {
                return true; // Assume eligible on error
            }
        }

        /// <summary>
        /// Refresh exfil statuses from game memory.
        /// Call this periodically to update Open/Pending/Closed status.
        /// </summary>
        public void Refresh()
        {
            try
            {
                if (_exits is null)
                    Init();
                
                if (_exits is null || _exits.Count == 0)
                    return;

                var map = Memory.CreateScatterMap();
                var round1 = map.AddRound();

                for (int ix = 0; ix < _exits.Count; ix++)
                {
                    int i = ix;
                    var entry = _exits[i];
                    
                    if (entry is Exfil exfil && exfil.ExfilBase != 0)
                    {
                        round1.PrepareReadValue<int>(exfil.ExfilBase + Offsets.ExfiltrationPoint._status);
                        round1.Completed += (sender, s1) =>
                        {
                            if (s1.ReadValue<int>(exfil.ExfilBase + Offsets.ExfiltrationPoint._status, out var exfilStatus))
                            {
                                exfil.Update((Enums.EExfiltrationStatus)exfilStatus);
                            }
                        };
                    }
                }
                
                map.Execute();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[ExitManager] Refresh Error: {ex.Message}");
            }
        }

        #region IReadOnlyCollection

        public int Count => _exits?.Count ?? 0;
        public IEnumerator<IExitPoint> GetEnumerator() => _exits?.GetEnumerator() ?? Enumerable.Empty<IExitPoint>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}