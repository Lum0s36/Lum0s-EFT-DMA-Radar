/*
 * Quest Helper ViewModel - Complete Implementation
 * Combines LONE's QuestManager with full UI features
 */

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov;
using LoneEftDmaRadar.Tarkov.GameWorld.Quests;

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

        private async Task StartPeriodicRefreshAsync()
        {
            while (true)
            {
                await Task.Delay(2000); // Refresh every 2 seconds
                
                try
                {
                    if (!Memory.InRaid)
                        continue;
                    
                    var questManager = Memory.Game?.QuestManager;
                    if (questManager == null)
                        continue;
                    
                    // Force QuestManager to refresh from game memory
                    try
                    {
                        questManager.Refresh(CancellationToken.None);
                    }
                    catch { /* Ignore refresh errors */ }
                    
                    var activeQuests = questManager.Quests;
                    
                    await System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                    {
                        var activeIds = new HashSet<string>(activeQuests.Keys, StringComparer.OrdinalIgnoreCase);
                        
                        foreach (var quest in AllQuests)
                        {
                            quest.IsActive = activeIds.Contains(quest.QuestId);
                            
                            // Refresh progress for expanded quests
                            if (quest.IsExpanded)
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
            // Delay to allow game memory to stabilize
            Task.Delay(2000).ContinueWith(_ =>
            {
                System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    _currentMapId = Memory.MapID;
                    _isInRaid = Memory.InRaid;
                    
                    // Force immediate progress refresh
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

        /// <summary>
        /// Forces a refresh of all quest progress counters from game memory.
        /// </summary>
        private void ForceRefreshAllQuestProgress()
        {
            try
            {
                var questManager = Memory.Game?.QuestManager;
                if (questManager == null)
                    return;
                
                // Force QuestManager to refresh counters
                questManager.Refresh(CancellationToken.None);
                
                var activeQuests = questManager.Quests;
                var activeIds = new HashSet<string>(activeQuests.Keys, StringComparer.OrdinalIgnoreCase);
                
                foreach (var quest in AllQuests)
                {
                    quest.IsActive = activeIds.Contains(quest.QuestId);
                    
                    // Refresh expanded quests
                    if (quest.IsExpanded)
                        quest.RefreshObjectiveCompletionStatus();
                }
            }
            catch (Exception ex)
            {
                LoneEftDmaRadar.UI.Misc.DebugLogger.LogDebug($"[QuestHelperViewModel] Error refreshing quest progress: {ex.Message}");
            }
        }

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
                return $"Map: {GetMapName(_currentMapId)} | Showing {filteredCount} active quests ({activeCount} total active)";
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

            // Get active quest IDs from game memory
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

            if (KappaOnly && !IsKappaQuest(quest.QuestId))
                return false;

            if (LightkeeperOnly && !IsLightkeeperQuest(quest.QuestId))
                return false;

            if (!string.IsNullOrEmpty(SelectedMapFilter) && SelectedMapFilter != "All Maps")
            {
                if (!HasObjectivesOnMap(quest.QuestId, MapDisplayNameToId(SelectedMapFilter)))
                    return false;
            }

            if (_isInRaid && ActiveOnly)
            {
                if (!quest.IsActive) 
                    return false;
                
                if (!string.IsNullOrEmpty(_currentMapId) && !HasObjectivesOnMap(quest.QuestId, _currentMapId)) 
                    return false;
            }

            return true;
        }

        private static string MapDisplayNameToId(string displayName) => displayName switch
        {
            "Customs" => "bigmap",
            "Factory" => "factory4_day",
            "Ground Zero" => "sandbox",
            "Interchange" => "interchange",
            "Labyrinth" => "labyrinth",
            "Labs" => "laboratory",
            "Lighthouse" => "lighthouse",
            "Reserve" => "rezervbase",
            "Shoreline" => "shoreline",
            "Streets" => "tarkovstreets",
            "Woods" => "woods",
            _ => displayName?.ToLowerInvariant() ?? ""
        };

        private static bool IsKappaQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return false;
            if (!TarkovDataManager.TaskData.TryGetValue(questId, out var task)) return false;
            return task.KappaRequired;
        }

        private static bool IsLightkeeperQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return false;
            if (!TarkovDataManager.TaskData.TryGetValue(questId, out var task)) return false;
            return task.LightkeeperRequired || 
                   (task.Trader?.Name?.Equals("Lightkeeper", StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private static bool HasObjectivesOnMap(string questId, string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return true;
            if (!TarkovDataManager.TaskData.TryGetValue(questId, out var task)) return false;

            if (task.Map?.NameId != null)
                return IsMapMatch(task.Map.NameId, mapId);

            bool hasMapSpecificObjective = false;
            bool hasAnyMapObjective = false;

            if (task.Objectives != null)
            {
                foreach (var obj in task.Objectives)
                {
                    if (obj.Maps != null && obj.Maps.Count > 0)
                    {
                        foreach (var objMap in obj.Maps)
                        {
                            if (objMap?.NameId != null)
                            {
                                hasMapSpecificObjective = true;
                                if (IsMapMatch(objMap.NameId, mapId))
                                    hasAnyMapObjective = true;
                            }
                        }
                    }

                    if (obj.Zones != null)
                    {
                        foreach (var zone in obj.Zones)
                        {
                            if (zone?.Map?.NameId != null)
                            {
                                hasMapSpecificObjective = true;
                                if (IsMapMatch(zone.Map.NameId, mapId))
                                    hasAnyMapObjective = true;
                            }
                        }
                    }
                }
            }

            if (hasMapSpecificObjective)
                return hasAnyMapObjective;

            return true;
        }

        private static bool IsMapMatch(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) return true;
            if (a.Contains("factory", StringComparison.OrdinalIgnoreCase) && 
                b.Contains("factory", StringComparison.OrdinalIgnoreCase)) return true;
            if (a.Contains("sandbox", StringComparison.OrdinalIgnoreCase) && 
                b.Contains("sandbox", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
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

        private static string GetMapName(string mapId) => mapId switch
        {
            "factory4_day" => "Factory (Day)",
            "factory4_night" => "Factory (Night)",
            "bigmap" => "Customs",
            "interchange" => "Interchange",
            "woods" => "Woods",
            "shoreline" => "Shoreline",
            "rezervbase" => "Reserve",
            "laboratory" => "Labs",
            "labyrinth" => "Labyrinth",
            "lighthouse" => "Lighthouse",
            "tarkovstreets" => "Streets",
            "sandbox" or "sandbox_high" => "Ground Zero",
            _ => mapId ?? "Unknown"
        };

        #endregion
    }

    /// <summary>
    /// Quest entry for the tracking list with expandable objectives
    /// </summary>
    public sealed class QuestTrackingEntry : INotifyPropertyChanged
    {
        private readonly QuestHelperViewModel _parent;
        internal bool _isTracked;
        private bool _isActive;
        private bool _isExpanded;
        private bool _objectivesLoaded;
        private string _requiredKeysInfo;
        private string _requiredItemsInfo;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public QuestTrackingEntry(QuestHelperViewModel parent) => _parent = parent;

        public string QuestId { get; set; }
        public string QuestName { get; set; }
        public string TraderName { get; set; }
        public string MapId { get; set; }
        public int RequiredItemCount { get; set; }
        public int RequiredZoneCount { get; set; }

        public bool IsTracked
        {
            get => _isTracked;
            set 
            { 
                if (_isTracked != value)
                {
                    _isTracked = value; 
                    OnPropertyChanged(nameof(IsTracked)); 
                    _parent?.OnTrackingChanged();
                }
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(nameof(IsActive)); OnPropertyChanged(nameof(StatusColor)); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); OnPropertyChanged(nameof(ExpandButtonText)); }
        }

        public void ToggleExpanded()
        {
            IsExpanded = !IsExpanded;
            if (IsExpanded && !_objectivesLoaded)
                LoadObjectives();
            else if (IsExpanded)
                RefreshObjectiveCompletionStatus();
        }

        public void RefreshObjectiveCompletionStatus()
        {
            var questManager = Memory.Game?.QuestManager;
            if (questManager == null)
            {
                LoneEftDmaRadar.UI.Misc.DebugLogger.LogDebug($"[QuestTrackingEntry] QuestManager is null");
                return;
            }
            
            if (!questManager.Quests.TryGetValue(QuestId, out var questEntry))
            {
                LoneEftDmaRadar.UI.Misc.DebugLogger.LogDebug($"[QuestTrackingEntry] Quest {QuestId} not found in active quests");
                return;
            }
            
            int completedCount = 0;
            int updatedCount = 0;
            
            foreach (var obj in Objectives)
            {
                if (string.IsNullOrEmpty(obj.ObjectiveId))
                    continue;
                
                // Check if completed
                var wasCompleted = obj.IsCompleted;
                var isCompleted = questEntry.IsObjectiveCompleted(obj.ObjectiveId);
                obj.IsCompleted = isCompleted;
                
                if (isCompleted)
                    completedCount++;
                
                // Get current progress count
                var oldProgress = obj.CurrentCount;
                var progress = questEntry.GetObjectiveProgress(obj.ObjectiveId);
                obj.CurrentCount = progress;
                
                if (progress != oldProgress || wasCompleted != isCompleted)
                    updatedCount++;
            }
            
            if (updatedCount > 0 || completedCount > 0)
            {
                LoneEftDmaRadar.UI.Misc.DebugLogger.LogDebug(
                    $"[QuestTrackingEntry] Quest {QuestName}: {completedCount} completed, {updatedCount} updated");
            }
        }

        public string ExpandButtonText => IsExpanded ? "-" : "+";
        public Brush StatusColor => IsActive ? Brushes.ForestGreen : Brushes.Transparent;
        
        public string RequirementsInfo
        {
            get
            {
                var parts = new List<string>();
                if (RequiredItemCount > 0) parts.Add($"{RequiredItemCount} items");
                if (RequiredZoneCount > 0) parts.Add($"{RequiredZoneCount} zones");
                if (!string.IsNullOrEmpty(MapId)) parts.Add(MapId);
                return parts.Count > 0 ? string.Join(" | ", parts) : "No requirements";
            }
        }

        public string ObjectivesHeaderText
        {
            get
            {
                if (string.IsNullOrEmpty(_requiredKeysInfo) && string.IsNullOrEmpty(_requiredItemsInfo))
                    return "Objectives:";
                
                var parts = new List<string> { "Objectives:" };
                if (!string.IsNullOrEmpty(_requiredKeysInfo))
                    parts.Add(_requiredKeysInfo);
                if (!string.IsNullOrEmpty(_requiredItemsInfo))
                    parts.Add(_requiredItemsInfo);
                return string.Join(" | ", parts);
            }
        }

        public string TooltipText => $"{TraderName} - {QuestName}\n{RequirementsInfo}";
        public bool HasNoObjectives => _objectivesLoaded && Objectives.Count == 0;

        public ObservableCollection<QuestObjectiveEntry> Objectives { get; } = new();

        private void LoadObjectives()
        {
            _objectivesLoaded = true;
            Objectives.Clear();
            
            var keyNames = new List<string>();
            var questItemNames = new List<string>();
            var regularItemNames = new List<string>();

            if (!TarkovDataManager.TaskData.TryGetValue(QuestId, out var task))
            {
                OnPropertyChanged(nameof(HasNoObjectives));
                OnPropertyChanged(nameof(ObjectivesHeaderText));
                return;
            }
            
            // Get quest progress from memory
            var questManager = Memory.Game?.QuestManager;
            LoneEftDmaRadar.Tarkov.GameWorld.Quests.QuestEntry questEntry = null;
            questManager?.Quests.TryGetValue(QuestId, out questEntry);

            // Collect NeededKeys
            if (task.NeededKeys != null)
            {
                foreach (var keyGroup in task.NeededKeys)
                {
                    if (keyGroup.Keys != null)
                    {
                        foreach (var key in keyGroup.Keys)
                        {
                            if (!string.IsNullOrEmpty(key.Name))
                                keyNames.Add(key.Name);
                        }
                    }
                }
            }

            // Process objectives
            if (task.Objectives != null)
            {
                foreach (var obj in task.Objectives)
                {
                    int count = obj.Count;
                    
                    if (obj.Type == QuestObjectiveType.Shoot && count <= 0 && !string.IsNullOrEmpty(obj.Description))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(obj.Description, @"\b(\d+)\b");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed) && parsed > 0 && parsed < 1000)
                            count = parsed;
                    }
                    
                    if ((obj.Type == QuestObjectiveType.FindItem || obj.Type == QuestObjectiveType.GiveItem) && count <= 0)
                        count = 1;

                    if (obj.RequiredKeys != null)
                    {
                        foreach (var keyGroup in obj.RequiredKeys)
                        {
                            foreach (var key in keyGroup)
                            {
                                if (!string.IsNullOrEmpty(key.Name))
                                    keyNames.Add(key.Name);
                            }
                        }
                    }

                    if (obj.QuestItem != null && !string.IsNullOrEmpty(obj.QuestItem.Name))
                        questItemNames.Add(obj.QuestItem.Name);

                    if ((obj.Type == QuestObjectiveType.FindItem || obj.Type == QuestObjectiveType.GiveItem) && obj.Item != null)
                    {
                        var itemText = obj.Item.Name ?? obj.Item.ShortName;
                        if (count > 1) itemText += $" x{count}";
                        if (!string.IsNullOrEmpty(itemText))
                            regularItemNames.Add(itemText);
                    }

                    // Get initial completion/progress state from memory
                    bool isCompleted = false;
                    int currentCount = 0;
                    
                    if (questEntry != null && !string.IsNullOrEmpty(obj.Id))
                    {
                        isCompleted = questEntry.IsObjectiveCompleted(obj.Id);
                        currentCount = questEntry.GetObjectiveProgress(obj.Id);
                    }

                    Objectives.Add(new QuestObjectiveEntry
                    {
                        ObjectiveId = obj.Id,
                        Type = obj.Type,
                        Description = obj.Description ?? GetDefaultDescription(obj),
                        Count = count,
                        TypeIcon = GetIcon(obj.Type),
                        ZoneCount = obj.Zones?.Count ?? 0,
                        ItemName = obj.Item?.Name ?? obj.QuestItem?.Name,
                        MapName = obj.Maps?.FirstOrDefault()?.Name,
                        IsCompleted = isCompleted,
                        CurrentCount = currentCount
                    });
                }
            }
            
            // Build header info
            var distinctKeys = keyNames.Distinct().ToList();
            if (distinctKeys.Count > 0)
            {
                _requiredKeysInfo = "KEY: " + string.Join(", ", distinctKeys.Take(2));
                if (distinctKeys.Count > 2)
                    _requiredKeysInfo += $" (+{distinctKeys.Count - 2})";
            }

            var allItems = new List<string>();
            foreach (var qi in questItemNames.Distinct().Take(2))
                allItems.Add($"Q:{qi}");
            foreach (var ri in regularItemNames.Distinct().Take(2))
                allItems.Add(ri);
            
            if (allItems.Count > 0)
            {
                _requiredItemsInfo = "ITEM: " + string.Join(", ", allItems.Take(2));
                int remaining = allItems.Count - 2;
                if (remaining > 0)
                    _requiredItemsInfo += $" (+{remaining})";
            }

            OnPropertyChanged(nameof(HasNoObjectives));
            OnPropertyChanged(nameof(ObjectivesHeaderText));
        }

        private static string GetDefaultDescription(TarkovDataManager.TaskElement.ObjectiveElement obj)
        {
            return obj.Type switch
            {
                QuestObjectiveType.FindItem => $"Find {obj.Item?.Name ?? obj.Item?.ShortName ?? "item"}" + (obj.Count > 1 ? $" x{obj.Count}" : "") + (obj.FoundInRaid ? " (FIR)" : ""),
                QuestObjectiveType.GiveItem => $"Hand over {obj.Item?.Name ?? obj.Item?.ShortName ?? "item"}" + (obj.Count > 1 ? $" x{obj.Count}" : ""),
                QuestObjectiveType.FindQuestItem => $"Find quest item: {obj.QuestItem?.Name ?? "unknown"}",
                QuestObjectiveType.GiveQuestItem => $"Hand over quest item: {obj.QuestItem?.Name ?? "unknown"}",
                QuestObjectiveType.Visit => $"Visit location" + (obj.Zones?.Count > 0 ? $" ({obj.Zones.Count} zones)" : ""),
                QuestObjectiveType.Mark => $"Mark location" + (obj.MarkerItem != null ? $" with {obj.MarkerItem.Name}" : ""),
                QuestObjectiveType.PlantItem => $"Plant {obj.MarkerItem?.Name ?? "item"}",
                QuestObjectiveType.PlantQuestItem => $"Plant quest item",
                QuestObjectiveType.Shoot => $"Eliminate {(obj.Count > 0 ? $"{obj.Count} " : "")}targets",
                QuestObjectiveType.Extract => "Survive and extract",
                QuestObjectiveType.BuildWeapon => "Build weapon",
                QuestObjectiveType.Skill => $"Level skill",
                QuestObjectiveType.TraderLevel => $"Reach trader level",
                _ => obj._type ?? "Complete objective"
            };
        }

        private static string GetIcon(QuestObjectiveType type) => type switch
        {
            QuestObjectiveType.FindItem or QuestObjectiveType.FindQuestItem => "FIND",
            QuestObjectiveType.GiveItem or QuestObjectiveType.GiveQuestItem => "GIVE",
            QuestObjectiveType.Visit => "GO",
            QuestObjectiveType.Mark or QuestObjectiveType.PlantItem or QuestObjectiveType.PlantQuestItem => "MARK",
            QuestObjectiveType.Shoot => "KILL",
            QuestObjectiveType.Extract => "EXIT",
            QuestObjectiveType.BuildWeapon => "BUILD",
            QuestObjectiveType.Skill => "SKILL",
            QuestObjectiveType.TraderLevel => "LVL",
            _ => "TASK"
        };
    }

    /// <summary>
    /// Quest objective entry
    /// </summary>
    public sealed class QuestObjectiveEntry : INotifyPropertyChanged
    {
        private bool _isCompleted;
        private int _currentCount;

        public event PropertyChangedEventHandler PropertyChanged;

        public string ObjectiveId { get; set; }
        public QuestObjectiveType Type { get; set; }
        public string TypeIcon { get; set; }
        public string Description { get; set; }
        public int Count { get; set; }
        public int ZoneCount { get; set; }
        public string ItemName { get; set; }
        public string MapName { get; set; }

        public int CurrentCount
        {
            get => _currentCount;
            set 
            { 
                _currentCount = value; 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCount)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressText)));
            }
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set 
            { 
                _isCompleted = value; 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCompleted)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextColor)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundColor)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextDecoration)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressText)));
            }
        }

        public string ProgressText
        {
            get
            {
                if (IsCompleted) return "DONE";
                if (Type == QuestObjectiveType.Shoot) return $"{CurrentCount}/{(Count > 0 ? Count : "?")}";
                if (Count > 0) return $"{CurrentCount}/{Count}";
                return "";
            }
        }

        public bool HasProgress => Type == QuestObjectiveType.Shoot || Count > 0;
        public Brush TextColor => IsCompleted ? Brushes.LimeGreen : Brushes.White;
        public Brush BackgroundColor => IsCompleted 
            ? new SolidColorBrush(Color.FromRgb(26, 58, 26)) 
            : new SolidColorBrush(Color.FromRgb(30, 30, 30));
        public TextDecorationCollection TextDecoration => IsCompleted ? TextDecorations.Strikethrough : null;

        public string AdditionalInfo
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(ItemName)) parts.Add($"Item: {ItemName}");
                if (ZoneCount > 0) parts.Add($"Zones: {ZoneCount}");
                if (!string.IsNullOrEmpty(MapName)) parts.Add($"Map: {MapName}");
                return parts.Count > 0 ? string.Join(" | ", parts) : null;
            }
        }

        public bool HasAdditionalInfo => !string.IsNullOrEmpty(AdditionalInfo);
    }
}
