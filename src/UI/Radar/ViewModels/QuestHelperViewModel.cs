/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using System.Collections.ObjectModel;
using System.ComponentModel;
using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov;
using LoneEftDmaRadar.UI.Radar.ViewModels.QuestHelper;

namespace LoneEftDmaRadar.UI.Radar.ViewModels
{
    public sealed class QuestHelperViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string _currentMapId;
        private bool _isInRaid;

        public QuestHelperViewModel()
        {
            MemDMA.RaidStarted += OnRaidStarted;
            MemDMA.RaidStopped += OnRaidStopped;

            RefreshAll();
            _ = StartPeriodicRefreshAsync();
        }

        #region Periodic Refresh

        private async Task StartPeriodicRefreshAsync()
        {
            while (true)
            {
                await Task.Delay(2000);

                try
                {
                    if (!Memory.InRaid)
                        continue;

                    var questManager = Memory.Game?.QuestManager;
                    if (questManager == null)
                        continue;

                    try { questManager.Refresh(CancellationToken.None); }
                    catch { }

                    var activeQuests = questManager.Quests;
                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    if (dispatcher == null) continue;

                    await dispatcher.InvokeAsync(() =>
                    {
                        var activeIds = new HashSet<string>(activeQuests.Keys, StringComparer.OrdinalIgnoreCase);

                        foreach (var quest in AllQuests)
                        {
                            quest.IsActive = activeIds.Contains(quest.QuestId);
                            if (quest.IsActive)
                                quest.RefreshObjectiveCompletionStatus();
                        }

                        if (_currentMapId != Memory.MapID)
                        {
                            _currentMapId = Memory.MapID;
                            OnPropertyChanged(nameof(ActiveOnlyStatusText));
                        }

                        _isInRaid = true;
                        ApplyFilter();
                        UpdateInfo();
                    });
                }
                catch { }
            }
        }

        private void OnRaidStarted(object sender, EventArgs e)
        {
            Task.Delay(2000).ContinueWith(_ =>
            {
                System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    _currentMapId = Memory.MapID;
                    _isInRaid = Memory.InRaid;
                    ForceRefreshAllQuestProgress();
                    RefreshAll();
                    OnPropertyChanged(nameof(ActiveOnlyStatusText));
                });
            });
        }

        private void OnRaidStopped(object sender, EventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                _currentMapId = null;
                _isInRaid = false;

                foreach (var quest in AllQuests)
                    quest.IsActive = false;

                UpdateInfo();
                OnPropertyChanged(nameof(ActiveOnlyStatusText));

                if (ActiveOnly)
                    ApplyFilter();
            });
        }

        private void ForceRefreshAllQuestProgress()
        {
            try
            {
                var questManager = Memory.Game?.QuestManager;
                if (questManager == null) return;

                questManager.Refresh(CancellationToken.None);

                var activeQuests = questManager.Quests;
                var activeIds = new HashSet<string>(activeQuests.Keys, StringComparer.OrdinalIgnoreCase);

                foreach (var quest in AllQuests)
                {
                    quest.IsActive = activeIds.Contains(quest.QuestId);
                    if (quest.IsExpanded)
                        quest.RefreshObjectiveCompletionStatus();
                }
            }
            catch (Exception ex)
            {
                LoneEftDmaRadar.UI.Misc.DebugLogger.LogDebug($"[QuestHelperViewModel] Error refreshing quest progress: {ex.Message}");
            }
        }

        #endregion

        #region Config Properties

        public bool Enabled
        {
            get => App.Config.QuestHelper.Enabled;
            set { App.Config.QuestHelper.Enabled = value; OnPropertyChanged(nameof(Enabled)); }
        }

        public bool ShowLocations
        {
            get => App.Config.QuestHelper.ShowLocations;
            set { App.Config.QuestHelper.ShowLocations = value; OnPropertyChanged(nameof(ShowLocations)); }
        }

        public bool ShowWidget
        {
            get => App.Config.QuestHelper.ShowWidget;
            set { App.Config.QuestHelper.ShowWidget = value; OnPropertyChanged(nameof(ShowWidget)); }
        }

        public bool ShowQuestItems
        {
            get => App.Config.Loot.ShowQuestItems;
            set
            {
                App.Config.Loot.ShowQuestItems = value;
                OnPropertyChanged(nameof(ShowQuestItems));
                App.Config.UI.EspQuestLoot = value;
                Memory.Game?.Loot?.RefreshFilter();
            }
        }

        public float ZoneDrawDistance
        {
            get => App.Config.QuestHelper.ZoneDrawDistance;
            set { App.Config.QuestHelper.ZoneDrawDistance = value; OnPropertyChanged(nameof(ZoneDrawDistance)); }
        }

        #endregion

        #region Filter Properties

        private string _searchFilter = "";
        public string SearchFilter
        {
            get => _searchFilter;
            set { _searchFilter = value; OnPropertyChanged(nameof(SearchFilter)); ApplyFilter(); }
        }

        public bool ActiveOnly
        {
            get => App.Config.QuestHelper.ActiveOnly;
            set
            {
                App.Config.QuestHelper.ActiveOnly = value;
                OnPropertyChanged(nameof(ActiveOnly));
                OnPropertyChanged(nameof(ActiveOnlyStatusText));
                OnPropertyChanged(nameof(IsMapFilterEnabled));

                if (value && !string.IsNullOrEmpty(SelectedMapFilter) && SelectedMapFilter != "All Maps")
                {
                    _selectedMapFilter = "All Maps";
                    OnPropertyChanged(nameof(SelectedMapFilter));
                }

                ApplyFilter();
            }
        }

        public bool KappaOnly
        {
            get => App.Config.QuestHelper.KappaOnly;
            set
            {
                App.Config.QuestHelper.KappaOnly = value;
                OnPropertyChanged(nameof(KappaOnly));

                if (value && App.Config.QuestHelper.LightkeeperOnly)
                {
                    App.Config.QuestHelper.LightkeeperOnly = false;
                    OnPropertyChanged(nameof(LightkeeperOnly));
                }

                ApplyFilter();
            }
        }

        public bool LightkeeperOnly
        {
            get => App.Config.QuestHelper.LightkeeperOnly;
            set
            {
                App.Config.QuestHelper.LightkeeperOnly = value;
                OnPropertyChanged(nameof(LightkeeperOnly));

                if (value && App.Config.QuestHelper.KappaOnly)
                {
                    App.Config.QuestHelper.KappaOnly = false;
                    OnPropertyChanged(nameof(KappaOnly));
                }

                ApplyFilter();
            }
        }

        private string _selectedMapFilter = "All Maps";
        public string SelectedMapFilter
        {
            get => _selectedMapFilter;
            set { _selectedMapFilter = value ?? "All Maps"; OnPropertyChanged(nameof(SelectedMapFilter)); ApplyFilter(); }
        }

        public bool IsMapFilterEnabled => !ActiveOnly;

        public ObservableCollection<string> MapFilterOptions { get; } = new()
        {
            "All Maps", "Customs", "Factory", "Ground Zero", "Interchange",
            "Labyrinth", "Labs", "Lighthouse", "Reserve", "Shoreline", "Streets", "Woods"
        };

        public string ActiveOnlyStatusText
        {
            get
            {
                if (!ActiveOnly)
                    return "Showing all quests";
                if (!_isInRaid)
                    return "Not in raid - showing all quests";

                var activeCount = AllQuests.Count(q => q.IsActive);
                var filteredCount = FilteredQuests.Count;
                return $"Map: {QuestFilterHelper.GetMapName(_currentMapId)} | Showing {filteredCount} active quests ({activeCount} total active)";
            }
        }

        #endregion

        #region Quest Data

        public ObservableCollection<QuestTrackingEntry> AllQuests { get; } = new();
        public ObservableCollection<QuestTrackingEntry> FilteredQuests { get; } = new();

        public int TrackedQuestCount => AllQuests.Count(q => q.IsTracked);
        public int TotalQuestCount => AllQuests.Count;
        public int VisibleQuestCount => FilteredQuests.Count;

        private string _selectAllButtonText = "Select All";
        public string SelectAllButtonText
        {
            get => _selectAllButtonText;
            set { _selectAllButtonText = value; OnPropertyChanged(nameof(SelectAllButtonText)); }
        }

        private string _databaseInfo = "Loading...";
        public string DatabaseInfo
        {
            get => _databaseInfo;
            set { _databaseInfo = value; OnPropertyChanged(nameof(DatabaseInfo)); }
        }

        private string _activeQuestsInfo = "No active quests";
        public string ActiveQuestsInfo
        {
            get => _activeQuestsInfo;
            set { _activeQuestsInfo = value; OnPropertyChanged(nameof(ActiveQuestsInfo)); }
        }

        #endregion

        #region Public Methods

        public void RefreshAll()
        {
            var currentlyTracked = AllQuests.Where(q => q.IsTracked).Select(q => q.QuestId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            LoadQuests();

            foreach (var quest in AllQuests)
            {
                if (currentlyTracked.Contains(quest.QuestId))
                    quest._isTracked = true;
            }

            ApplyFilter();
            UpdateInfo();
        }

        public async Task RefreshFromApiAsync()
        {
            try
            {
                var currentlyTracked = AllQuests.Where(q => q.IsTracked).Select(q => q.QuestId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                await TarkovDataManager.RefreshFromApiAsync();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    LoadQuests();
                    foreach (var quest in AllQuests)
                    {
                        if (currentlyTracked.Contains(quest.QuestId))
                            quest._isTracked = true;
                    }
                    ApplyFilter();
                    UpdateInfo();
                });
            }
            catch
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => RefreshAll());
            }
        }

        public void ToggleSelectAll()
        {
            bool allSelected = FilteredQuests.All(q => q.IsTracked);
            foreach (var quest in FilteredQuests)
                quest.IsTracked = !allSelected;

            SaveTrackedQuests();
            UpdateSelectAllButton();
            UpdateCounts();
        }

        public void OnTrackingChanged()
        {
            SaveTrackedQuests();
            UpdateSelectAllButton();
            UpdateCounts();
        }

        #endregion

        #region Private Methods

        private void LoadQuests()
        {
            AllQuests.Clear();

            if (TarkovDataManager.TaskData == null || TarkovDataManager.TaskData.Count == 0)
            {
                DatabaseInfo = "Quest database not loaded";
                return;
            }

            var zoneCount = TarkovDataManager.TaskZones?.Values.Sum(z => z.Count) ?? 0;
            DatabaseInfo = $"Quest Database: {TarkovDataManager.TaskData.Count} quests, {zoneCount} zones";

            var questManager = Memory.Game?.QuestManager;
            var activeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (questManager != null)
            {
                foreach (var q in questManager.Quests.Keys)
                    activeIds.Add(q);
            }

            var trackedIds = App.Config.QuestHelper.TrackedQuests;

            foreach (var task in TarkovDataManager.TaskData.Values.OrderBy(t => t.Trader?.Name).ThenBy(t => t.Name))
            {
                bool isActive = activeIds.Contains(task.Id);
                var entry = new QuestTrackingEntry(this)
                {
                    QuestId = task.Id,
                    QuestName = task.Name,
                    TraderName = task.Trader?.Name ?? "Unknown",
                    MapId = task.Map?.NameId,
                    RequiredItemCount = task.Objectives?.Count(o => o.Item != null || o.QuestItem != null) ?? 0,
                    RequiredZoneCount = task.Objectives?.Sum(o => o.Zones?.Count ?? 0) ?? 0,
                    IsActive = isActive,
                    IsTracked = trackedIds.Count == 0 ? isActive : trackedIds.Contains(task.Id)
                };
                AllQuests.Add(entry);
            }
        }

        private void ApplyFilter()
        {
            FilteredQuests.Clear();

            foreach (var quest in AllQuests)
            {
                if (!PassesFilter(quest)) continue;
                FilteredQuests.Add(quest);
            }

            UpdateSelectAllButton();
            UpdateCounts();
            OnPropertyChanged(nameof(ActiveOnlyStatusText));
        }

        private bool PassesFilter(QuestTrackingEntry quest)
        {
            if (!string.IsNullOrWhiteSpace(SearchFilter))
            {
                if (!quest.QuestName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) &&
                    !quest.TraderName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (KappaOnly && !QuestFilterHelper.IsKappaQuest(quest.QuestId))
                return false;

            if (LightkeeperOnly && !QuestFilterHelper.IsLightkeeperQuest(quest.QuestId))
                return false;

            if (!string.IsNullOrEmpty(SelectedMapFilter) && SelectedMapFilter != "All Maps")
            {
                if (!QuestFilterHelper.HasObjectivesOnMap(quest.QuestId, QuestFilterHelper.MapDisplayNameToId(SelectedMapFilter)))
                    return false;
            }

            if (_isInRaid && ActiveOnly)
            {
                if (!quest.IsActive)
                    return false;

                if (!string.IsNullOrEmpty(_currentMapId) && !QuestFilterHelper.HasObjectivesOnMap(quest.QuestId, _currentMapId))
                    return false;
            }

            return true;
        }

        private void SaveTrackedQuests()
        {
            App.Config.QuestHelper.TrackedQuests.Clear();
            foreach (var q in AllQuests.Where(x => x._isTracked))
                App.Config.QuestHelper.TrackedQuests.Add(q.QuestId);

            _ = App.Config.SaveAsync();
        }

        private void UpdateSelectAllButton()
        {
            bool all = FilteredQuests.Count > 0 && FilteredQuests.All(q => q.IsTracked);
            SelectAllButtonText = all ? "Deselect All" : "Select All";
        }

        private void UpdateCounts()
        {
            OnPropertyChanged(nameof(TrackedQuestCount));
            OnPropertyChanged(nameof(TotalQuestCount));
            OnPropertyChanged(nameof(VisibleQuestCount));
        }

        private void UpdateInfo()
        {
            _currentMapId = Memory.MapID;
            _isInRaid = Memory.InRaid;

            if (!_isInRaid)
            {
                ActiveQuestsInfo = "Not in raid";
                return;
            }

            if (Memory.Game?.QuestManager == null)
            {
                ActiveQuestsInfo = "Waiting for quest data...";
                return;
            }

            var tracked = AllQuests.Where(q => q.IsTracked && q.IsActive).ToList();
            if (tracked.Count == 0)
            {
                var activeCount = AllQuests.Count(q => q.IsActive);
                ActiveQuestsInfo = activeCount > 0
                    ? $"{activeCount} active quests (none tracked)"
                    : "No active quests found";
                return;
            }

            ActiveQuestsInfo = $"Tracked Quests: {tracked.Count}\n" +
                              string.Join("\n", tracked.Take(5).Select(q => $"  > {q.QuestName}"));
            if (tracked.Count > 5)
                ActiveQuestsInfo += $"\n  ... and {tracked.Count - 5} more";
        }

        #endregion
    }
}
