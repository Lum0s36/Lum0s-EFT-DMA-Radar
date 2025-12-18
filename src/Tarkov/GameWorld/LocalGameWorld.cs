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
using LoneEftDmaRadar.Misc;
using LoneEftDmaRadar.Misc.Workers;
using LoneEftDmaRadar.Tarkov.Features.MemWrites;
using LoneEftDmaRadar.Tarkov.GameWorld.Exits;
using LoneEftDmaRadar.Tarkov.GameWorld.Explosives;
using LoneEftDmaRadar.Tarkov.GameWorld.Hazards;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Quests;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Misc;
using VmmSharpEx.Options;

namespace LoneEftDmaRadar.Tarkov.GameWorld
{
    /// <summary>
    /// Class containing Game (Raid) instance.
    /// IDisposable.
    /// </summary>
    public sealed class LocalGameWorld : IDisposable
    {
        #region Fields / Properties / Constructors

        public static implicit operator ulong(LocalGameWorld x) => x.Base;

        /// <summary>
        /// LocalGameWorld Address.
        /// </summary>
        private ulong Base { get; }

        private readonly RegisteredPlayers _rgtPlayers;
        private readonly ExitManager _exfilManager;
        private readonly ExplosivesManager _explosivesManager;
        private readonly WorkerThread _t1;
        private readonly WorkerThread _t2;
        private readonly WorkerThread _t3;
        private readonly WorkerThread _t4;
        private readonly MemWritesManager _memWritesManager;
        private readonly QuestManager _questManager;

        /// <summary>
        /// Map ID of Current Map.
        /// </summary>
        public string MapID { get; }

        public bool InRaid => !_disposed;
        public IReadOnlyCollection<AbstractPlayer> Players => _rgtPlayers;
        public IReadOnlyCollection<IExplosiveItem> Explosives => _explosivesManager;
        public IReadOnlyCollection<IExitPoint> Exits => _exfilManager;
        public LocalPlayer LocalPlayer => _rgtPlayers?.LocalPlayer;
        public LootManager Loot { get; }
        
        /// <summary>
        /// Quest Manager for tracking active quests and zones.
        /// </summary>
        public QuestManager QuestManager => _questManager;

        /// <summary>
        /// World Hazards (minefields, radiation zones, etc.) for the current map.
        /// </summary>
        public IReadOnlyList<IWorldHazard> Hazards { get; }

        private LocalGameWorld() { }

        /// <summary>
        /// Game Constructor.
        /// Only called internally.
        /// </summary>
        private LocalGameWorld(ulong localGameWorld, string mapID)
        {
            try
            {
                Base = localGameWorld;
                MapID = mapID;
                _t1 = new WorkerThread()
                {
                    Name = "Realtime Worker",
                    ThreadPriority = ThreadPriority.AboveNormal,
                    SleepDuration = TimeSpan.FromMilliseconds(8),
                    SleepMode = WorkerThreadSleepMode.DynamicSleep
                };
                _t1.PerformWork += RealtimeWorker_PerformWork;
                _t2 = new WorkerThread()
                {
                    Name = "Slow Worker",
                    ThreadPriority = ThreadPriority.BelowNormal,
                    SleepDuration = TimeSpan.FromMilliseconds(50)
                };
                _t2.PerformWork += SlowWorker_PerformWork;
                _t3 = new WorkerThread()
                {
                    Name = "Explosives Worker",
                    SleepDuration = TimeSpan.FromMilliseconds(16), // ~60Hz for smooth grenade tracking
                    SleepMode = WorkerThreadSleepMode.DynamicSleep
                };
                _t3.PerformWork += ExplosivesWorker_PerformWork;
                var rgtPlayersAddr = Memory.ReadPtr(localGameWorld + Offsets.GameWorld.RegisteredPlayers, false);
                _rgtPlayers = new RegisteredPlayers(rgtPlayersAddr, this);
                ArgumentOutOfRangeException.ThrowIfLessThan(_rgtPlayers.GetPlayerCount(), 1, nameof(_rgtPlayers));
                Loot = new(localGameWorld);
                // ExitManager now reads from game memory for live status updates
                _exfilManager = new ExitManager(localGameWorld, mapID, _rgtPlayers.LocalPlayer);
                _explosivesManager = new(localGameWorld);
                _memWritesManager = new MemWritesManager();
                // QuestManager needs the LocalPlayer's Profile pointer
                var profilePtr = _rgtPlayers.LocalPlayer?.Profile ?? 0;
                _questManager = new QuestManager(profilePtr);
                Hazards = GetHazards(mapID);
                _t4 = new WorkerThread()
                {
                    Name = "MemWrites Worker",
                    ThreadPriority = ThreadPriority.Normal,
                    SleepDuration = TimeSpan.FromMilliseconds(100)
                };
                _t4.PerformWork += MemWritesWorker_PerformWork;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Loads hazard data for the specified map from TarkovDataManager.
        /// </summary>
        private static List<IWorldHazard> GetHazards(string mapId)
        {
            var list = new List<IWorldHazard>();
            if (TarkovDataManager.MapData?.TryGetValue(mapId, out var mapData) == true)
            {
                if (mapData.Hazards != null)
                {
                    foreach (var hazard in mapData.Hazards)
                    {
                        list.Add(new GenericWorldHazard
                        {
                            HazardType = hazard.HazardType,
                            Position = hazard.Position?.AsVector3() ?? Vector3.Zero
                        });
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Start all Game Threads.
        /// </summary>
        public void Start()
        {
            _memWritesManager?.OnRaidStart();
            _t1.Start();
            _t2.Start();
            _t3.Start();
            _t4.Start();
        }

        /// <summary>
        /// Blocks until a LocalGameWorld Singleton Instance can be instantiated.
        /// </summary>
        public static LocalGameWorld CreateGameInstance(CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                ResourceJanitor.Run();
                Memory.ThrowIfProcessNotRunning();
                try
                {
                    var instance = GetLocalGameWorld(ct);
                    DebugLogger.LogDebug("Raid has started!");
                    return instance;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (ex.InnerException?.Message?.Contains("GameWorld not found") == true)
                {
                    // Expected when not in raid - silently continue polling
                }
                catch (Exception ex)
                {
                    DebugLogger.LogDebug($"ERROR Instantiating Game Instance: {ex}");
                }
                finally
                {
                    Thread.Sleep(1000);
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Checks if a Raid has started.
        /// Loads Local Game World resources.
        /// </summary>
        /// <returns>True if Raid has started, otherwise False.</returns>
        private static LocalGameWorld GetLocalGameWorld(CancellationToken ct)
        {
            try
            {
                /// Get LocalGameWorld
                var localGameWorld = GameObjectManager.Get().GetGameWorld(ct, out string map);
                if (localGameWorld == 0) throw new Exception("GameWorld Address is 0");
                return new LocalGameWorld(localGameWorld, map);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("ERROR Getting LocalGameWorld", ex);
            }
        }

        /// <summary>
        /// Main Game Loop executed by Memory Worker Thread. Refreshes/Updates Player List and performs Player Allocations.
        /// </summary>
        public void Refresh()
        {
            try
            {
                ThrowIfRaidEnded();
                if (MapID.Equals("tarkovstreets", StringComparison.OrdinalIgnoreCase) ||
                    MapID.Equals("woods", StringComparison.OrdinalIgnoreCase))
                    TryAllocateBTR();
                _rgtPlayers.Refresh(); // Check for new players, add to list, etc.
            }
            catch (OperationCanceledException ex) // Raid Ended
            {
                DebugLogger.LogDebug(ex.Message);
                Dispose();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"CRITICAL ERROR - Raid ended due to unhandled exception: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Throws an exception if the current raid instance has ended.
        /// </summary>
        /// <exception cref="OperationCanceledException"></exception>
        private void ThrowIfRaidEnded()
        {
            for (int i = 0; i < 5; i++) // Re-attempt if read fails -- 5 times
            {
                try
                {
                    if (!IsRaidActive())
                        continue;
                    return;
                }
                catch { Thread.Sleep(10); } // short delay between read attempts
            }
            throw new OperationCanceledException("Raid has ended!"); // Still not valid? Raid must have ended.
        }

        /// <summary>
        /// Checks if the Current Raid is Active, and LocalPlayer is alive/active.
        /// </summary>
        /// <returns>True if raid is active, otherwise False.</returns>
        private bool IsRaidActive()
        {
            try
            {
                var mainPlayer = Memory.ReadPtr(this + Offsets.GameWorld.MainPlayer, false);
                ArgumentOutOfRangeException.ThrowIfNotEqual(mainPlayer, _rgtPlayers.LocalPlayer, nameof(mainPlayer));
                return _rgtPlayers.GetPlayerCount() > 0;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Realtime Thread T1

        /// <summary>
        /// Managed Worker Thread that does realtime (player position/info) updates.
        /// </summary>
        private void RealtimeWorker_PerformWork(object sender, WorkerThreadArgs e)
        {
            // Filter active and alive players
            // Note: BTR is always included so its position can be updated
            var players = _rgtPlayers.Where(x => 
                x.IsActive && 
                x.IsAlive);
            
            var localPlayer = LocalPlayer;
            if (!players.Any()) // No players - Throttle
            {
                Thread.Sleep(1);
                return;
            }

            using var scatter = Memory.CreateScatter(VmmFlags.NOCACHE);
            if (MemDMA.CameraManager != null && localPlayer != null)
            {
                MemDMA.CameraManager.OnRealtimeLoop(scatter, localPlayer);
            }
            foreach (var player in players)
            {
                player.OnRealtimeLoop(scatter);
            }
            scatter.Execute();
        }

        #endregion

        #region Slow Thread T2

        /// <summary>
        /// Managed Worker Thread that does ~Slow Local Game World Updates.
        /// *** THIS THREAD HAS A LONG RUN TIME! LOOPS ~MAY~ TAKE ~10 SECONDS OR MORE ***
        /// </summary>
        private void SlowWorker_PerformWork(object sender, WorkerThreadArgs e)
        {
            var ct = e.CancellationToken;
            ValidatePlayerTransforms(); // Check for transform anomalies
            // Sync FilteredLoot
            Loot.Refresh(ct);
            // Refresh player equipment
            RefreshEquipment();
            // Refresh Wishlist
            RefreshWishlist();
            // Refresh Quest zones and objectives
            RefreshQuests(ct);
            // Refresh Exfil statuses (Open/Pending/Closed)
            RefreshExfils();
        }

        /// <summary>
        /// Refreshes the exfil status from game memory.
        /// </summary>
        private void RefreshExfils()
        {
            try
            {
                _exfilManager?.Refresh();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[ExitManager] ERROR Refreshing: {ex}");
            }
        }

        /// <summary>
        /// Refreshes the quest manager with active quests and zones.
        /// </summary>
        private void RefreshQuests(CancellationToken ct)
        {
            if (!App.Config.QuestHelper.Enabled)
                return;

            try
            {
                _questManager?.Refresh(ct);
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[QuestManager] ERROR Refreshing: {ex}");
            }
        }

        /// <summary>
        /// Refreshes the wishlist from game memory.
        /// </summary>
        private void RefreshWishlist()
        {
            if (!App.Config.Loot.ShowWishlistedRadar)
                return;

            try
            {
                LocalPlayer?.RefreshWishlist();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[Wishlist] ERROR Refreshing: {ex}");
            }
        }

        private void RefreshEquipment()
        {
            // Refresh ALL ObservedPlayer equipment (human and AI, living and dead)
            // This ensures:
            // - Human player gear is tracked
            // - AI player "!!" wishlist markers update when items are looted
            // - Dead player/corpse equipment stays current
            var allPlayers = _rgtPlayers.OfType<ObservedPlayer>();
            foreach (var player in allPlayers)
            {
                player.Equipment.Refresh();
            }
        }

        public void ValidatePlayerTransforms()
        {
            try
            {
                var players = _rgtPlayers
                    .Where(x => x.IsActive && x.IsAlive && x is not BtrPlayer);
                if (players.Any()) // at least 1 player
                {
                    using var map = Memory.CreateScatterMap();
                    var round1 = map.AddRound();
                    var round2 = map.AddRound();
                    foreach (var player in players)
                    {
                        player.OnValidateTransforms(round1, round2);
                    }
                    map.Execute(); // execute scatter read
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"CRITICAL ERROR - ValidatePlayerTransforms Loop FAILED: {ex}");
            }
        }

        #endregion

        #region MemWrites Thread T4

        private void MemWritesWorker_PerformWork(object sender, WorkerThreadArgs e)
        {
            try
            {
                if (!App.Config.MemWrites.Enabled)
                {
                    Thread.Sleep(100);
                    return;
                }

                var localPlayer = LocalPlayer;
                if (localPlayer is null)
                    return;

                _memWritesManager.Apply(localPlayer);
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"[MemWritesWorker] Error: {ex}");
            }
        }

        #endregion

        #region Explosives Thread T3

        /// <summary>
        /// Managed Worker Thread that does Explosives (grenades,etc.) updates.
        /// </summary>
        private void ExplosivesWorker_PerformWork(object sender, WorkerThreadArgs e)
        {
            _explosivesManager.Refresh(e.CancellationToken);
        }

        #endregion

        #region BTR Vehicle

        /// <summary>
        /// Checks if there is a Bot attached to the BTR Turret and re-allocates the player instance.
        /// </summary>
        public void TryAllocateBTR()
        {
            try
            {
                if (_rgtPlayers.Any(p => p is BtrPlayer))
                    return;

                var btrController = Memory.ReadPtr(this + Offsets.GameWorld.BtrController);
                var btrView = Memory.ReadPtr(btrController + Offsets.BtrController.BtrView);
                var btrTurretView = Memory.ReadPtr(btrView + Offsets.BTRView.turret);
                var btrOperator = Memory.ReadPtr(btrTurretView + Offsets.BTRTurretView._bot);
                _rgtPlayers.TryAllocateBTR(btrView, btrOperator);
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"ERROR Allocating BTR: {ex}");
            }
        }

        #endregion

        #region IDisposable

        private bool _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, true) == false)
            {
                _t1?.Dispose();
                _t2?.Dispose();
                _t3?.Dispose();
                _t4?.Dispose();
            }
        }

        #endregion
    }
}
