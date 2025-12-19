/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using Collections.Pooled;
using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov;
using LoneEftDmaRadar.Tarkov.GameWorld.Exits;
using LoneEftDmaRadar.Tarkov.GameWorld.Explosives;
using LoneEftDmaRadar.Tarkov.GameWorld.Hazards;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Quests;
using LoneEftDmaRadar.UI.Loot;
using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.UI.Radar.Maps;
using LoneEftDmaRadar.UI.Radar.Views;
using LoneEftDmaRadar.UI.Radar.ViewModels.Helpers;
using LoneEftDmaRadar.UI.Skia;
using SkiaSharp.Views.WPF;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LoneEftDmaRadar.UI.Radar.ViewModels
{
    public sealed class RadarViewModel
    {
        #region Static Game State (delegated to IGameStateProvider)

        private static IGameStateProvider GameState => MemoryGameStateProvider.Instance;

        private static bool Starting => Memory?.Starting ?? false;
        private static bool Ready => Memory?.Ready ?? false;
        private static bool InRaid => GameState.InRaid;
        private static string MapID => GameState.MapID ?? "null";
        private static LocalPlayer LocalPlayer => GameState.LocalPlayer;
        private static IEnumerable<LootItem> Loot => GameState.Loot?.FilteredLoot;
        private static IEnumerable<StaticLootContainer> Containers => GameState.Loot?.AllLoot?.OfType<StaticLootContainer>();
        private static IReadOnlyCollection<AbstractPlayer> AllPlayers => GameState.Players;
        private static IReadOnlyCollection<IExplosiveItem> Explosives => GameState.Explosives;
        private static IReadOnlyCollection<IExitPoint> Exits => GameState.Exits;
        private static IReadOnlyList<IWorldHazard> Hazards => Memory?.Hazards;

        private static bool FilterIsSet => !string.IsNullOrEmpty(LootFilter.SearchString);

        private static bool LootCorpsesVisible =>
            (MainWindow.Instance?.Settings?.ViewModel?.ShowLoot ?? false) &&
            !(MainWindow.Instance?.Radar?.Overlay?.ViewModel?.HideCorpses ?? false) &&
            !FilterIsSet;

        private IEnumerable<IMouseoverEntity> MouseOverItems
        {
            get
            {
                var players = AllPlayers?
                    .Where(x => x is not Tarkov.GameWorld.Player.LocalPlayer && !x.HasExfild && (LootCorpsesVisible ? x.IsAlive : true))
                    ?? Enumerable.Empty<AbstractPlayer>();

                var loot = Loot ?? Enumerable.Empty<IMouseoverEntity>();
                var containers = Containers ?? Enumerable.Empty<IMouseoverEntity>();
                var exits = Exits ?? Enumerable.Empty<IMouseoverEntity>();

                var questLocations = (App.Config.QuestHelper.Enabled && App.Config.QuestHelper.ShowLocations)
                    ? (GameState.QuestManager?.LocationConditions?.Values ?? Enumerable.Empty<IMouseoverEntity>())
                    : Enumerable.Empty<IMouseoverEntity>();

                if (FilterIsSet && !(MainWindow.Instance?.Radar?.Overlay?.ViewModel?.HideCorpses ?? false))
                    players = players.Where(x => x.LootObject is null || !loot.Contains(x.LootObject));

                var result = loot.Concat(containers).Concat(players).Concat(exits).Concat(questLocations);
                return result.Any() ? result : null;
            }
        }

        /// <summary>
        /// Currently 'Moused Over' Group.
        /// </summary>
        public static int? MouseoverGroup => _instance?._mouseHandler?.MouseoverGroup;

        /// <summary>
        /// Currently 'Moused Over' Item.
        /// </summary>
        public static IMouseoverEntity CurrentMouseoverItem => _instance?._mouseHandler?.MouseOverItem;

        private static RadarViewModel _instance;

        #endregion

        #region Fields/Properties

        private readonly RadarTab _parent;
        private readonly PeriodicTimer _periodicTimer = new(TimeSpan.FromSeconds(1));
        private int _fps;
        private long _lastRadarFrameTicks;
        private DispatcherTimer _renderTimer;
        private string _lastTabHeader;
        private int _appliedMaxFps;

        // Helper classes
        private readonly RadarWidgetManager _widgetManager;
        private readonly RadarStatusRenderer _statusRenderer = new();
        private readonly RadarPingEffectManager _pingManager = new();
        private readonly RadarMouseHandler _mouseHandler;

        /// <summary>
        /// Skia Radar Viewport.
        /// </summary>
        public SKGLElement Radar => _parent.Radar;

        // Widget accessors (delegated to widget manager)
        public AimviewWidget AimviewWidget => _widgetManager?.AimviewWidget;
        public PlayerInfoWidget InfoWidget => _widgetManager?.InfoWidget;
        public LootInfoWidget LootInfoWidget => _widgetManager?.LootInfoWidget;
        public QuestHelperWidget QuestHelperWidget => _widgetManager?.QuestHelperWidget;

        #endregion

        #region Constructor

        public RadarViewModel(RadarTab parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _instance = this;

            _widgetManager = new RadarWidgetManager(Radar);
            _mouseHandler = new RadarMouseHandler(
                Radar,
                () => InRaid,
                () => MouseOverItems,
                () => _widgetManager.ClearAllWidgetFocus());

            _lastRadarFrameTicks = Stopwatch.GetTimestamp();
            _ = OnStartupAsync();
            _ = RunPeriodicTimerAsync();
        }

        private async Task OnStartupAsync()
        {
            await _parent.Dispatcher.Invoke(async () =>
            {
                while (Radar.GRContext is null)
                    await Task.Delay(10);

                Radar.GRContext.SetResourceCacheLimit(512 * 1024 * 1024);
                _mouseHandler.UpdateDpiScaleFactors();
                _widgetManager.InitializeWidgets();

                // Subscribe to item click event for ping effect
                if (_widgetManager.LootInfoWidget != null)
                    _widgetManager.LootInfoWidget.ItemClickedForPing += LootInfoWidget_ItemClickedForPing;

                Radar.PaintSurface += Radar_PaintSurface;
                ConfigureRenderLoop();
            });
        }

        private void ConfigureRenderLoop()
        {
            int maxFps = App.Config.UI.RadarMaxFPS;
            _appliedMaxFps = maxFps;

            if (maxFps > 0)
            {
                Radar.RenderContinuously = false;
                _renderTimer?.Stop();
                _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(Math.Max(1.0, 1000.0 / maxFps))
                };
                _renderTimer.Tick += (_, __) =>
                {
                    try { Radar.InvalidateVisual(); } catch { }
                };
                _renderTimer.Start();
            }
            else
            {
                _renderTimer?.Stop();
                _renderTimer = null;
                Radar.RenderContinuously = true;
            }
        }

        #endregion

        #region Render Loop

        private void Radar_PaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
        {
            var isStarting = Starting;
            var isReady = Ready;
            var inRaid = InRaid;
            var canvas = e.Surface.Canvas;

            // FPS cap
            int maxFps = App.Config.UI.RadarMaxFPS;
            if (maxFps > 0 && Radar.RenderContinuously)
            {
                long now = Stopwatch.GetTimestamp();
                double elapsedMs = (now - _lastRadarFrameTicks) * 1000.0 / Stopwatch.Frequency;
                double targetMs = 1000.0 / maxFps;
                double waitMs = targetMs - elapsedMs;
                if (waitMs > 0)
                {
                    Thread.Sleep((int)Math.Min(waitMs, 50));
                    now = Stopwatch.GetTimestamp();
                }
                _lastRadarFrameTicks = now;
            }
            else
            {
                _lastRadarFrameTicks = Stopwatch.GetTimestamp();
            }

            try
            {
                Interlocked.Increment(ref _fps);
                SetMapName();

                string mapID = MapID;
                if (!mapID.Equals(EftMapManager.Map?.ID, StringComparison.OrdinalIgnoreCase))
                {
                    EftMapManager.LoadMap(mapID);
                }

                canvas.Clear();

                if (inRaid && LocalPlayer is LocalPlayer localPlayer)
                {
                    DrawRaidContent(canvas, e, localPlayer);
                }
                else
                {
                    DrawStatusMessage(canvas, isStarting, isReady, inRaid);
                }

                canvas.Flush();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"***** CRITICAL RENDER ERROR: {ex}");
            }
        }

        private void DrawRaidContent(SKCanvas canvas, SKPaintGLSurfaceEventArgs e, LocalPlayer localPlayer)
        {
            var map = EftMapManager.Map;
            ArgumentNullException.ThrowIfNull(map, nameof(map));

            var closestToMouse = _mouseHandler.MouseOverItem;
            var localPlayerPos = GetValidPlayerPosition(localPlayer);
            var localPlayerMapPos = localPlayerPos.ToMapPos(map.Config);

            // Update map setup helper coordinates
            if (MainWindow.Instance?.Radar?.MapSetupHelper?.ViewModel is MapSetupHelperViewModel mapSetup && mapSetup.IsVisible)
            {
                mapSetup.Coords = $"Unity X,Y,Z: {localPlayerPos.X},{localPlayerPos.Y},{localPlayerPos.Z}";
            }

            // Get map parameters
            EftMapParams mapParams;
            bool mapFree = MainWindow.Instance?.Radar?.Overlay?.ViewModel?.IsMapFreeEnabled ?? false;

            if (mapFree)
            {
                if (_mouseHandler.MapPanPosition == default)
                    _mouseHandler.MapPanPosition = localPlayerMapPos;
                var panPos = _mouseHandler.MapPanPosition;
                mapParams = map.GetParameters(Radar, App.Config.UI.Zoom, ref panPos);
            }
            else
            {
                _mouseHandler.MapPanPosition = default;
                mapParams = map.GetParameters(Radar, App.Config.UI.Zoom, ref localPlayerMapPos);
            }

            var info = e.RawInfo;
            var mapCanvasBounds = new SKRect(info.Rect.Left, info.Rect.Top, info.Rect.Right, info.Rect.Bottom);

            // Draw map
            map.Draw(canvas, localPlayer.Position.Y, mapParams.Bounds, mapCanvasBounds);

            // Draw game elements
            DrawLoot(canvas, mapParams, localPlayer);
            DrawMines(canvas, mapParams);
            DrawExplosives(canvas, mapParams, localPlayer);
            DrawHazards(canvas, mapParams, localPlayer);
            DrawExits(canvas, mapParams, localPlayer);
            DrawPlayers(canvas, mapParams, localPlayer, map);
            DrawQuestLocations(canvas, mapParams, localPlayer);

            // Draw local player on top
            localPlayer.Draw(canvas, mapParams, localPlayer);

            // Draw widgets
            DrawWidgets(canvas, localPlayer, closestToMouse, mapParams);

            // Draw ping effects
            _pingManager.DrawPingEffects(canvas, mapParams);
        }

        private void DrawLoot(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            if (!App.Config.Loot.Enabled)
                return;

            if (Loot?.Reverse() is IEnumerable<LootItem> loot)
            {
                foreach (var item in loot)
                {
                    if (App.Config.Loot.HideCorpses && item is LootCorpse)
                        continue;
                    item.Draw(canvas, mapParams, localPlayer);
                }
            }

            if (App.Config.Containers.Enabled && Containers is IEnumerable<StaticLootContainer> containers)
            {
                var containerConfig = App.Config.Containers;
                foreach (var container in containers)
                {
                    var id = container.ID ?? "NULL";
                    if (containerConfig.SelectAll || containerConfig.Selected.ContainsKey(id))
                    {
                        container.Draw(canvas, mapParams, localPlayer);
                    }
                }
            }
        }

        private void DrawMines(SKCanvas canvas, EftMapParams mapParams)
        {
            if (!App.Config.UI.ShowMines)
                return;

            var map = EftMapManager.Map;
            if (StaticGameData.Mines.TryGetValue(MapID, out var mines))
            {
                foreach (ref var mine in mines.Span)
                {
                    var mineZoomedPos = mine.ToMapPos(map.Config).ToZoomedPos(mapParams);
                    mineZoomedPos.DrawMineMarker(canvas);
                }
            }
        }

        private void DrawExplosives(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            if (Explosives is IReadOnlyCollection<IExplosiveItem> explosives)
            {
                foreach (var explosive in explosives)
                {
                    explosive.Draw(canvas, mapParams, localPlayer);
                }
            }
        }

        private void DrawHazards(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            if (App.Config.UI.ShowHazards && Hazards is IReadOnlyList<IWorldHazard> hazards)
            {
                foreach (var hazard in hazards)
                {
                    hazard.Draw(canvas, mapParams, localPlayer);
                }
            }
        }

        private void DrawExits(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            if (App.Config.UI.ShowExfils && Exits is IReadOnlyCollection<IExitPoint> exits)
            {
                foreach (var exit in exits)
                {
                    exit.Draw(canvas, mapParams, localPlayer);
                }
            }
        }

        private void DrawPlayers(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer, IEftMap map)
        {
            var allPlayers = AllPlayers?.Where(x => !x.HasExfild);
            if (allPlayers is null)
                return;

            foreach (var player in allPlayers)
            {
                if (player == localPlayer)
                    continue;
                player.Draw(canvas, mapParams, localPlayer);
            }

            // Connect groups
            if (App.Config.UI.ConnectGroups)
            {
                var groupedPlayers = allPlayers.Where(x => x.IsHumanHostileActive && x.GroupID != -1);
                using var groups = groupedPlayers.Select(x => x.GroupID).ToPooledSet();

                foreach (var grp in groups)
                {
                    var grpMembers = groupedPlayers.Where(x => x.GroupID == grp);
                    if (grpMembers?.Any() == true)
                    {
                        var combinations = grpMembers
                            .SelectMany(x => grpMembers, (x, y) =>
                                Tuple.Create(
                                    x.Position.ToMapPos(map.Config).ToZoomedPos(mapParams),
                                    y.Position.ToMapPos(map.Config).ToZoomedPos(mapParams)));

                        foreach (var pair in combinations)
                        {
                            canvas.DrawLine(pair.Item1.X, pair.Item1.Y, pair.Item2.X, pair.Item2.Y, SKPaints.PaintConnectorGroup);
                        }
                    }
                }
            }
        }

        private void DrawQuestLocations(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            if (!App.Config.QuestHelper.Enabled || !App.Config.QuestHelper.ShowLocations)
                return;

            var questManager = GameState.QuestManager;
            if (questManager != null)
            {
                foreach (var location in questManager.LocationConditions.Values)
                {
                    location.Draw(canvas, mapParams, localPlayer);
                }
            }
        }

        private void DrawWidgets(SKCanvas canvas, LocalPlayer localPlayer, IMouseoverEntity closestToMouse, EftMapParams mapParams)
        {
            if (AllPlayers is not null && App.Config.InfoWidget.Enabled)
            {
                InfoWidget?.Draw(canvas, localPlayer, AllPlayers);
            }

            closestToMouse?.DrawMouseover(canvas, mapParams, localPlayer);

            if (App.Config.AimviewWidget.Enabled)
            {
                AimviewWidget?.Draw(canvas);
            }

            if (App.Config.LootInfoWidget.Enabled)
            {
                LootInfoWidget?.Draw(canvas, Loot);
            }

            if (App.Config.QuestHelper.ShowWidget)
            {
                QuestHelperWidget?.Draw(canvas);
            }
        }

        private void DrawStatusMessage(SKCanvas canvas, bool isStarting, bool isReady, bool inRaid)
        {
            if (!isStarting)
                _statusRenderer.DrawGameNotRunning(canvas);
            else if (isStarting && !isReady)
                _statusRenderer.DrawStartingUp(canvas);
            else if (!inRaid)
                _statusRenderer.DrawWaitingForRaid(canvas);
        }

        #endregion

        #region Public Methods

        public void PurgeSKResources()
        {
            _parent.Dispatcher.Invoke(() =>
            {
                Radar.GRContext?.PurgeResources();
            });
        }

        public void ZoomIn(int amt)
        {
            App.Config.UI.Zoom = Math.Max(1, App.Config.UI.Zoom - amt);
        }

        public void ZoomOut(int amt)
        {
            App.Config.UI.Zoom = Math.Min(200, App.Config.UI.Zoom + amt);
        }

        #endregion

        #region Private Methods

        private void SetMapName()
        {
            string map = EftMapManager.Map?.Config?.Name;
            string name = map is null ? "Radar" : $"Radar ({map})";
            if (_lastTabHeader == name)
                return;
            if (MainWindow.Instance?.RadarTab is TabItem tab)
            {
                tab.Header = name;
                _lastTabHeader = name;
            }
        }

        private async Task RunPeriodicTimerAsync()
        {
            while (await _periodicTimer.WaitForNextTickAsync())
            {
                _statusRenderer.IncrementStatusOrder();

                int cfgFps = App.Config.UI.RadarMaxFPS;
                if (cfgFps != _appliedMaxFps)
                {
                    _parent.Dispatcher.Invoke(ConfigureRenderLoop);
                }

                int fps = Interlocked.Exchange(ref _fps, 0);
                string title = $"{App.Name} ({fps} fps)";
                if (MainWindow.Instance is MainWindow mainWindow)
                {
                    mainWindow.Title = title;
                }
            }
        }

        private void LootInfoWidget_ItemClickedForPing(object sender, string itemName)
        {
            if (!InRaid || string.IsNullOrWhiteSpace(itemName) || EftMapManager.Map is null)
                return;

            var lootItems = Loot;
            if (lootItems is null)
                return;

            _pingManager.CreatePingsForItem(itemName, lootItems, EftMapManager.Map);
        }

        private static Vector3 GetValidPlayerPosition(LocalPlayer localPlayer)
        {
            var pos = localPlayer.Position;

            if (IsValidPosition(pos))
                return pos;

            var allPlayers = AllPlayers;
            if (allPlayers != null)
            {
                var btrPlayer = allPlayers.OfType<BtrPlayer>().FirstOrDefault();
                if (btrPlayer != null)
                {
                    var btrPos = btrPlayer.Position;
                    if (IsValidPosition(btrPos))
                        return btrPos;
                }
            }

            return pos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidPosition(Vector3 pos)
        {
            if (float.IsNaN(pos.X) || float.IsNaN(pos.Y) || float.IsNaN(pos.Z))
                return false;
            if (float.IsInfinity(pos.X) || float.IsInfinity(pos.Y) || float.IsInfinity(pos.Z))
                return false;
            if (pos.X == 0 && pos.Y == 0 && pos.Z == 0)
                return false;

            const float maxValidCoord = 5000f;
            if (Math.Abs(pos.X) > maxValidCoord || Math.Abs(pos.Z) > maxValidCoord)
                return false;

            return true;
        }

        #endregion
    }
}
