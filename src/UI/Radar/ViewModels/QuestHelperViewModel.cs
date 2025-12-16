/*
 * Quest Helper ViewModel - Complete Rewrite
 * Clean, simple implementation
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
            // Subscribe to raid events for automatic updates
            MemDMA.RaidStarted += OnRaidStarted;
            MemDMA.RaidStopped += OnRaidStopped;
            
            RefreshAll();
            
            // Start periodic refresh to pick up quest data from memory
            _ = StartPeriodicRefreshAsync();
        }

        /// <summary>
        /// Periodically refresh quest data when in raid to pick up updates from QuestManager
        /// </summary>
        private async Task StartPeriodicRefreshAsync()
        {
            while (true)
            {
                await Task.Delay(5000); // Every 5 seconds
                
                try
                {
                    // Only refresh if in raid and QuestManager has data
                    if (Memory.InRaid && Memory.Game?.QuestManager != null)
                    {
                        var questManager = Memory.Game.QuestManager;
                        var activeCount = questManager.ActiveQuests.Count();
                        
                        // Check if we need to update (active quests changed)
                        var currentActiveCount = AllQuests.Count(q => q.IsActive);
                        if (activeCount != currentActiveCount)
                        {
                            System.Diagnostics.Debug.WriteLine($"[QuestHelper] Periodic refresh: {activeCount} active quests (was {currentActiveCount})");
                            
                            await System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                            {
                                RefreshAll();
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[QuestHelper] Periodic refresh error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Called when a raid starts - refresh quest data and update map filter
        /// </summary>
        private void OnRaidStarted(object sender, EventArgs e)
        {
            try
            {
                // Use dispatcher to update UI from background thread
                System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    var mapId = Memory.MapID;
                    var isInRaid = Memory.InRaid;
                    
                    _currentMapId = mapId;
                    _isInRaid = isInRaid;
                    
                    // Automatically enable ActiveOnly filter when entering raid
                    if (!ActiveOnly)
                    {
                        App.Config.QuestHelper.ActiveOnly = true;
                        OnPropertyChanged(nameof(ActiveOnly));
                    }
                    
                    // Refresh all quest data when raid starts (to get active quests from game memory)
                    RefreshAll();
                    
                    OnPropertyChanged(nameof(ActiveOnlyStatusText));
                    
                    System.Diagnostics.Debug.WriteLine($"[QuestHelper] Raid started on map: {mapId}, filtering active quests");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[QuestHelper] OnRaidStarted error: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when a raid stops - clear raid-specific state
        /// </summary>
        private void OnRaidStopped(object sender, EventArgs e)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    _currentMapId = null;
                    _isInRaid = false;
                    
                    // Update the active quests info
                    UpdateInfo();
                    
                    OnPropertyChanged(nameof(ActiveOnlyStatusText));
                    
                    // Re-apply filter to show all quests again (if ActiveOnly was enabled)
                    if (ActiveOnly)
                        ApplyFilter();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[QuestHelper] OnRaidStopped error: {ex.Message}");
            }
        }

        #region Config Properties

        public bool Enabled
        {
            get => App.Config.QuestHelper.Enabled;
            set { App.Config.QuestHelper.Enabled = value; OnPropertyChanged(nameof(Enabled)); }
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

        /// <summary>
        /// When enabled, shows only active quests for the current map (requires being in raid).
        /// When disabled, shows all quests.
        /// </summary>
        public bool ActiveOnly
        {
            get => App.Config.QuestHelper.ActiveOnly;
            set 
            { 
                App.Config.QuestHelper.ActiveOnly = value; 
                OnPropertyChanged(nameof(ActiveOnly));
                OnPropertyChanged(nameof(ActiveOnlyStatusText));
                OnPropertyChanged(nameof(IsMapFilterEnabled)); // Map filter depends on this
                
                // Clear map filter when ActiveOnly is enabled (they don't make sense together)
                if (value && !string.IsNullOrEmpty(SelectedMapFilter) && SelectedMapFilter != "All Maps")
                {
                    _selectedMapFilter = "All Maps";
                    OnPropertyChanged(nameof(SelectedMapFilter));
                }
                
                ApplyFilter();
            }
        }

        /// <summary>
        /// When enabled, shows only quests required for Kappa container.
        /// </summary>
        public bool KappaOnly
        {
            get => App.Config.QuestHelper.KappaOnly;
            set 
            { 
                App.Config.QuestHelper.KappaOnly = value; 
                OnPropertyChanged(nameof(KappaOnly));
                
                // Disable Lightkeeper filter when Kappa is enabled (mutually exclusive)
                if (value && App.Config.QuestHelper.LightkeeperOnly)
                {
                    App.Config.QuestHelper.LightkeeperOnly = false;
                    OnPropertyChanged(nameof(LightkeeperOnly));
                }
                
                ApplyFilter();
            }
        }

        /// <summary>
        /// When enabled, shows only Lightkeeper quests.
        /// </summary>
        public bool LightkeeperOnly
        {
            get => App.Config.QuestHelper.LightkeeperOnly;
            set 
            { 
                App.Config.QuestHelper.LightkeeperOnly = value; 
                OnPropertyChanged(nameof(LightkeeperOnly));
                
                // Disable Kappa filter when Lightkeeper is enabled (mutually exclusive)
                if (value && App.Config.QuestHelper.KappaOnly)
                {
                    App.Config.QuestHelper.KappaOnly = false;
                    OnPropertyChanged(nameof(KappaOnly));
                }
                
                ApplyFilter();
            }
        }

        /// <summary>
        /// Selected map filter. Empty string or "All Maps" means no filter.
        /// </summary>
        private string _selectedMapFilter = "All Maps";
        public string SelectedMapFilter
        {
            get => _selectedMapFilter;
            set 
            { 
                _selectedMapFilter = value ?? "All Maps"; 
                OnPropertyChanged(nameof(SelectedMapFilter));
                ApplyFilter();
            }
        }

        /// <summary>
        /// Map filter is only enabled when ActiveOnly is disabled.
        /// When in raid with ActiveOnly, the map filter is automatic based on current map.
        /// </summary>
        public bool IsMapFilterEnabled => !ActiveOnly;

        /// <summary>
        /// Available maps for filtering.
        /// </summary>
        public ObservableCollection<string> MapFilterOptions { get; } = new()
        {
            "All Maps",
            "Customs",
            "Factory",
            "Ground Zero",
            "Interchange",
            "Labyrinth",
            "Labs",
            "Lighthouse",
            "Reserve",
            "Shoreline",
            "Streets",
            "Woods"
        };

        /// <summary>
        /// Status text showing the current filter state
        /// </summary>
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

        /// <summary>All quests from database</summary>
        public ObservableCollection<QuestTrackingEntry> AllQuests { get; } = new();

        /// <summary>Filtered quests for display</summary>
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

        #endregion

        #region Info Display

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

        /// <summary>Refresh everything</summary>
        public void RefreshAll()
        {
            // Save currently tracked quest IDs before refresh
            var currentlyTracked = AllQuests.Where(q => q.IsTracked).Select(q => q.QuestId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            LoadQuests();
            
            // Restore tracked state
            foreach (var quest in AllQuests)
            {
                if (currentlyTracked.Contains(quest.QuestId))
                {
                    quest._isTracked = true; // Set directly to avoid triggering save
                }
            }
            
            ApplyFilter();
            UpdateInfo();
        }

        /// <summary>Refresh with fresh data from API</summary>
        public async Task RefreshFromApiAsync()
        {
            try
            {
                // Save currently tracked quest IDs
                var currentlyTracked = AllQuests.Where(q => q.IsTracked).Select(q => q.QuestId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                
                // Try to fetch fresh data from API (this runs on background thread)
                await TarkovDataManager.RefreshFromApiAsync();
                
                // Switch back to UI thread for ObservableCollection updates
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    LoadQuests();
                    
                    // Restore tracked state
                    foreach (var quest in AllQuests)
                    {
                        if (currentlyTracked.Contains(quest.QuestId))
                        {
                            quest._isTracked = true;
                        }
                    }
                    
                    ApplyFilter();
                    UpdateInfo();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[QuestHelper] RefreshFromApiAsync failed: {ex.Message}");
                // If API fails, just do a normal refresh with cached data on UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => RefreshAll());
            }
        }

        /// <summary>Toggle select all visible quests</summary>
        public void ToggleSelectAll()
        {
            bool allSelected = FilteredQuests.All(q => q.IsTracked);
            foreach (var quest in FilteredQuests)
                quest.IsTracked = !allSelected;
            
            SaveTrackedQuests();
            UpdateSelectAllButton();
            UpdateCounts();
        }

        /// <summary>Called when quest tracking changes</summary>
        public void OnTrackingChanged()
        {
            SaveTrackedQuests();
            UpdateSelectAllButton();
            UpdateCounts();
        }

        /// <summary>Update current map for filtering</summary>
        public void UpdateCurrentMap(string mapId, bool isInRaid)
        {
            if (_currentMapId != mapId || _isInRaid != isInRaid)
            {
                _currentMapId = mapId;
                _isInRaid = isInRaid;
                OnPropertyChanged(nameof(ActiveOnlyStatusText));
                if (ActiveOnly)
                    ApplyFilter();
            }
        }

        #endregion

        #region Private Methods

        private void LoadQuests()
        {
            AllQuests.Clear();

            if (!QuestDatabase.IsInitialized)
            {
                DatabaseInfo = "Quest database not loaded";
                return;
            }

            DatabaseInfo = $"Quest Database: {QuestDatabase.AllQuests.Count} quests, {QuestDatabase.AllZones.Count} zones";

            // Get active quest IDs from game memory (only available in raid)
            var questManager = Memory.Game?.QuestManager;
            var activeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            if (questManager != null)
            {
                foreach (var q in questManager.ActiveQuests)
                {
                    activeIds.Add(q.Id);
                }
            }
            
            var trackedIds = App.Config.QuestHelper.TrackedQuests;

            System.Diagnostics.Debug.WriteLine($"[QuestHelper] LoadQuests: Found {activeIds.Count} active quests from memory, {trackedIds.Count} tracked");

            foreach (var quest in QuestDatabase.AllQuests.Values.OrderBy(q => q.TraderName).ThenBy(q => q.Name))
            {
                bool isActive = activeIds.Contains(quest.Id);
                var entry = new QuestTrackingEntry(this)
                {
                    QuestId = quest.Id,
                    QuestName = quest.Name,
                    TraderName = quest.TraderName ?? "Unknown",
                    MapId = quest.MapId,
                    RequiredItemCount = quest.RequiredItemIds?.Count ?? 0,
                    RequiredZoneCount = quest.RequiredZoneIds?.Count ?? 0,
                    IsActive = isActive,
                    IsTracked = trackedIds.Count == 0 ? isActive : trackedIds.Contains(quest.Id)
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
            
            System.Diagnostics.Debug.WriteLine($"[QuestHelper] ApplyFilter: {FilteredQuests.Count} quests passed filter (ActiveOnly={ActiveOnly}, InRaid={_isInRaid}, Map={_currentMapId})");
        }

        private bool PassesFilter(QuestTrackingEntry quest)
        {
            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchFilter))
            {
                if (!quest.QuestName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) &&
                    !quest.TraderName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Kappa filter - only show quests required for Kappa
            if (KappaOnly)
            {
                if (!IsKappaQuest(quest.QuestId))
                    return false;
            }

            // Lightkeeper filter - only show Lightkeeper quests
            if (LightkeeperOnly)
            {
                if (!IsLightkeeperQuest(quest.QuestId))
                    return false;
            }

            // Map filter - only show quests for selected map
            if (!string.IsNullOrEmpty(SelectedMapFilter) && SelectedMapFilter != "All Maps")
            {
                if (!HasObjectivesOnMap(quest.QuestId, MapDisplayNameToId(SelectedMapFilter)))
                    return false;
            }

            // ActiveOnly filter logic:
            // - If NOT in raid: show ALL quests (ignore ActiveOnly setting)
            // - If in raid AND ActiveOnly enabled: show only active quests for current map
            // - If in raid AND ActiveOnly disabled: show all quests
            if (_isInRaid && ActiveOnly)
            {
                // Must be an active quest (from LocalPlayer's quest list in memory)
                if (!quest.IsActive) 
                    return false;
                
                // Must have objectives on the current map
                if (!string.IsNullOrEmpty(_currentMapId) && !HasObjectivesOnMap(quest.QuestId, _currentMapId)) 
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Converts display map name to internal map ID.
        /// </summary>
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

        /// <summary>
        /// Checks if a quest is required for Kappa container.
        /// </summary>
        private static bool IsKappaQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return false;

            if (!TarkovDataManager.TaskData.TryGetValue(questId, out var task))
                return false;

            return task.KappaRequired;
        }

        /// <summary>
        /// Checks if a quest is required for Lightkeeper.
        /// </summary>
        private static bool IsLightkeeperQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return false;

            if (!TarkovDataManager.TaskData.TryGetValue(questId, out var task))
                return false;

            // Check both the API flag and the trader name
            return task.LightkeeperRequired || 
                   (task.Trader?.Name?.Equals("Lightkeeper", StringComparison.OrdinalIgnoreCase) ?? false);
        }

        /// <summary>
        /// Checks if a quest has any objectives that need to be completed on the specified map.
        /// Returns true if:
        /// - The quest's main map matches the current map
        /// - Any objective zone is on the current map
        /// - The quest has objectives that can be done anywhere (like "hand over items" without specific zone)
        /// </summary>
        private static bool HasObjectivesOnMap(string questId, string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) 
                return true;
            
            if (!TarkovDataManager.TaskData.TryGetValue(questId, out var task)) 
                return false; // Quest not found in data, don't show

            // Check if task has a specific map assigned
            if (task.Map?.NameId != null)
            {
                // Task is map-specific - only show if we're on that map
                return IsMapMatch(task.Map.NameId, mapId);
            }

            // Check objectives for map-specific zones
            bool hasMapSpecificObjective = false;
            bool hasAnyMapObjective = false;
            
            if (task.Objectives != null)
            {
                foreach (var obj in task.Objectives)
                {
                    // Check objective-level maps
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
                    
                    // Check objective zones
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
            
            // If quest has map-specific objectives, only show if at least one matches current map
            if (hasMapSpecificObjective)
                return hasAnyMapObjective;

            // Quest has no map restrictions (e.g., "hand over items to trader", "reach level X")
            // These quests are not shown for any specific map since they can be done anywhere/anytime
            // Only show them if we're not doing map-based filtering
            return false;
        }

        private static bool IsMapMatch(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return false;
            if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) return true;
            // Factory aliases (factory4_day, factory4_night)
            if (IsFactory(a) && IsFactory(b)) return true;
            // Ground Zero aliases (sandbox, sandbox_high)
            if (IsSandbox(a) && IsSandbox(b)) return true;
            return false;
        }

        private static bool IsFactory(string m) => m.Contains("factory", StringComparison.OrdinalIgnoreCase);
        private static bool IsSandbox(string m) => m.Contains("sandbox", StringComparison.OrdinalIgnoreCase);

        private void SaveTrackedQuests()
        {
            App.Config.QuestHelper.TrackedQuests.Clear();
            foreach (var q in AllQuests.Where(x => x._isTracked))
                App.Config.QuestHelper.TrackedQuests.Add(q.QuestId);
            
            // Save config to disk
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
            _isInRaid = Memory.Game?.QuestManager != null;

            if (!_isInRaid || Memory.Game?.QuestManager == null)
            {
                ActiveQuestsInfo = "No active quests (not in raid)";
                return;
            }

            var tracked = AllQuests.Where(q => q.IsTracked && q.IsActive).ToList();
            if (tracked.Count == 0)
            {
                ActiveQuestsInfo = "No tracked quests active";
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
    /// Quest entry for the tracking list
    /// </summary>
    public sealed class QuestTrackingEntry : INotifyPropertyChanged
    {
        private readonly QuestHelperViewModel _parent;
        internal bool _isTracked;  // internal so RefreshAll can set without triggering save
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
        }

        // Use simple ASCII characters that work in all fonts
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

        /// <summary>
        /// Header text showing required keys and items for quick reference
        /// Format: "Objectives: KEY: Room 114 | ITEM: Salewa x3"
        /// </summary>
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

            // First: Collect NeededKeys from task level (these are the keys required for the quest)
            if (task.NeededKeys != null)
            {
                foreach (var keyGroup in task.NeededKeys)
                {
                    if (keyGroup.Keys != null)
                    {
                        foreach (var key in keyGroup.Keys)
                        {
                            if (!string.IsNullOrEmpty(key.Name))
                            {
                                // Use the full name from the API for keys
                                keyNames.Add(key.Name);
                            }
                        }
                    }
                }
            }

            // Process objectives
            if (task.Objectives != null)
            {
                foreach (var obj in task.Objectives)
                {
                    // Determine the count
                    int count = obj.Count;
                    
                    // For shoot objectives, try to extract count from description if not set
                    if (obj.Type == QuestObjectiveType.Shoot && count <= 0 && !string.IsNullOrEmpty(obj.Description))
                    {
                        // Try to find numbers like "25 Scavs", "Eliminate 15", etc.
                        var match = System.Text.RegularExpressions.Regex.Match(obj.Description, @"\b(\d+)\b");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed) && parsed > 0 && parsed < 1000)
                            count = parsed;
                    }
                    
                    // Also check for giveItem/findItem if count not set
                    if ((obj.Type == QuestObjectiveType.FindItem || obj.Type == QuestObjectiveType.GiveItem) && count <= 0)
                    {
                        count = 1; // Default to 1 for find/give objectives
                    }

                    // Collect required keys from objective level
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

                    // Collect quest item info (special quest-specific items) - use FULL name
                    if (obj.QuestItem != null && !string.IsNullOrEmpty(obj.QuestItem.Name))
                    {
                        questItemNames.Add(obj.QuestItem.Name);
                    }

                    // Collect regular item info for give/find objectives - use FULL name
                    if ((obj.Type == QuestObjectiveType.FindItem || obj.Type == QuestObjectiveType.GiveItem) && obj.Item != null)
                    {
                        var itemText = obj.Item.Name ?? obj.Item.ShortName;
                        if (count > 1) itemText += $" x{count}";
                        if (!string.IsNullOrEmpty(itemText))
                            regularItemNames.Add(itemText);
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
                        MapName = obj.Maps?.FirstOrDefault()?.Name
                    });
                }
            }
            
            // Build header info with exact API names (full names)
            
            // Add keys (deduplicated)
            var distinctKeys = keyNames.Distinct().ToList();
            if (distinctKeys.Count > 0)
            {
                _requiredKeysInfo = "KEY: " + string.Join(", ", distinctKeys.Take(2));
                if (distinctKeys.Count > 2)
                    _requiredKeysInfo += $" (+{distinctKeys.Count - 2})";
            }
            else
            {
                _requiredKeysInfo = null;
            }

            // Add quest items (special items) - use Q: prefix
            var distinctQuestItems = questItemNames.Distinct().ToList();
            
            // Add regular items
            var distinctRegularItems = regularItemNames.Distinct().ToList();
            
            // Combine items for display
            var allItems = new List<string>();
            foreach (var qi in distinctQuestItems.Take(2))
                allItems.Add($"Q:{qi}");
            foreach (var ri in distinctRegularItems.Take(2))
                allItems.Add(ri);
            
            if (allItems.Count > 0)
            {
                _requiredItemsInfo = "ITEM: " + string.Join(", ", allItems.Take(2));
                int remaining = allItems.Count - 2;
                if (remaining > 0)
                    _requiredItemsInfo += $" (+{remaining})";
            }
            else
            {
                _requiredItemsInfo = null;
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

        /// <summary>
        /// Progress text - shows counter for countable objectives (kill, find, give, etc.)
        /// Always shows for Shoot objectives, even if count is 0
        /// </summary>
        public string ProgressText
        {
            get
            {
                if (IsCompleted)
                    return "DONE";
                
                // Always show counter for kill/shoot objectives
                if (Type == QuestObjectiveType.Shoot)
                    return $"{CurrentCount}/{(Count > 0 ? Count : "?")}";
                
                // Show counter for any objective with a count > 0
                if (Count > 0)
                    return $"{CurrentCount}/{Count}";
                
                return "";
            }
        }

        /// <summary>
        /// True if this objective has a visible progress counter
        /// </summary>
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
