/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Tarkov;
using LoneEftDmaRadar.UI.Loot;
using LoneEftDmaRadar.UI.Loot.Helpers;
using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.UI.Radar.Views;
using LoneEftDmaRadar.Web.TarkovDev.Data;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Input;
using SkiaSharp;
using System.Windows;

namespace LoneEftDmaRadar.UI.Radar.ViewModels
{
    public sealed class LootFiltersViewModel : INotifyPropertyChanged
    {
        #region Startup

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }

        private readonly LootFilterManager _filterManager;

        public LootFiltersViewModel(LootFiltersTab parent)
        {
            FilterNames = new ObservableCollection<string>(App.Config.LootFilters.Filters.Keys);
            AvailableItems = new ObservableCollection<TarkovMarketItem>(
                TarkovDataManager.AllItems.Values.OrderBy(x => x.Name));

            _filterManager = new LootFilterManager(FilterNames, name => SelectedFilterName = name);

            AddFilterCommand = new SimpleCommand(OnAddFilter);
            RenameFilterCommand = new SimpleCommand(OnRenameFilter);
            DeleteFilterCommand = new SimpleCommand(OnDeleteFilter);

            AddEntryCommand = new SimpleCommand(OnAddEntry);
            RemoveEntryCommand = new SimpleCommand(OnRemoveEntry);
            DeleteEntryCommand = new SimpleCommand(OnDeleteEntry);
            OpenItemSelectorCommand = new SimpleCommand(OnOpenItemSelector);
            ApplyColorToAllCommand = new SimpleCommand(OnApplyColorToAll);
            EnableAllEntriesCommand = new SimpleCommand(OnEnableAllEntries);
            ExportFiltersCommand = new SimpleCommand(OnExportFilters);
            ImportFiltersCommand = new SimpleCommand(OnImportFilters);

            if (FilterNames.Any())
                SelectedFilterName = App.Config.LootFilters.Selected;
            EnsureFirstItemSelected();
            LootFilterManager.RefreshLootFilter();
            parent.IsVisibleChanged += Parent_IsVisibleChanged;
        }

        private void Parent_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool visible && !visible)
            {
                LootFilterManager.RefreshLootFilter();
            }
        }

        #endregion

        #region Show Wishlisted

        public bool ShowWishlistedRadar
        {
            get => App.Config.Loot.ShowWishlistedRadar;
            set
            {
                if (App.Config.Loot.ShowWishlistedRadar != value)
                {
                    App.Config.Loot.ShowWishlistedRadar = value;
                    OnPropertyChanged();
                }
            }
        }

        public string WishlistColorRadar
        {
            get => App.Config.Loot.WishlistColorRadar;
            set
            {
                if (App.Config.Loot.WishlistColorRadar != value)
                {
                    App.Config.Loot.WishlistColorRadar = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Top Section - Filters

        private bool _currentFilterEnabled;
        public bool CurrentFilterEnabled
        {
            get => _currentFilterEnabled;
            set
            {
                if (_currentFilterEnabled == value) return;
                _currentFilterEnabled = value;
                App.Config.LootFilters.Filters[SelectedFilterName].Enabled = value;
                OnPropertyChanged();
            }
        }

        private string _currentFilterColor;
        public string CurrentFilterColor
        {
            get => _currentFilterColor;
            set
            {
                if (_currentFilterColor == value) return;
                _currentFilterColor = value;
                App.Config.LootFilters.Filters[SelectedFilterName].Color = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> FilterNames { get; }
        
        private string _selectedFilterName;
        public string SelectedFilterName
        {
            get => _selectedFilterName;
            set
            {
                if (_selectedFilterName == value) return;
                _selectedFilterName = value;
                App.Config.LootFilters.Selected = value;
                var userFilter = App.Config.LootFilters.Filters[value];
                CurrentFilterEnabled = userFilter.Enabled;
                CurrentFilterColor = userFilter.Color;
                Entries = userFilter.Entries;
                foreach (var entry in userFilter.Entries)
                    entry.ParentFilter = userFilter;
                EntrySearchText = string.Empty;
                ShowAllEntries = false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EntryCountText));
                OnPropertyChanged(nameof(ToggleAllButtonText));
            }
        }

        public ICommand AddFilterCommand { get; }
        private void OnAddFilter() => _filterManager.AddFilter();

        public ICommand RenameFilterCommand { get; }
        private void OnRenameFilter() => _filterManager.RenameFilter(SelectedFilterName);

        public ICommand DeleteFilterCommand { get; }
        private void OnDeleteFilter() => _filterManager.DeleteFilter(SelectedFilterName);

        public ICommand ExportFiltersCommand { get; }
        private void OnExportFilters()
        {
            LootFilterImportExportHelper.ExportFilters(MainWindow.Instance);
        }

        public ICommand ImportFiltersCommand { get; }
        private void OnImportFilters()
        {
            var result = LootFilterImportExportHelper.ImportFilters(
                MainWindow.Instance,
                SelectedFilterName,
                name => FilterNames.Add(name));

            if (result.IsSuccess)
            {
                if (result.Mode == ImportMode.Replace)
                {
                    // Refresh UI for replaced filter
                    var userFilter = App.Config.LootFilters.Filters[SelectedFilterName];
                    CurrentFilterEnabled = userFilter.Enabled;
                    CurrentFilterColor = userFilter.Color;
                    Entries = userFilter.Entries;
                    foreach (var entry in userFilter.Entries)
                        entry.ParentFilter = userFilter;
                }
                else if (result.FirstFilterName != null)
                {
                    // Select first imported filter
                    SelectedFilterName = result.FirstFilterName;
                }
                LootFilterManager.RefreshLootFilter();
            }
        }

        #endregion

        #region Bottom Section - Entries

        public ObservableCollection<TarkovMarketItem> AvailableItems { get; }
        
        private ICollectionView _filteredItems;
        public ICollectionView FilteredItems
        {
            get
            {
                if (_filteredItems == null)
                {
                    _filteredItems = CollectionViewSource.GetDefaultView(AvailableItems);
                    _filteredItems.Filter = FilterPredicate;
                }
                return _filteredItems;
            }
        }

        private TarkovMarketItem _selectedItemToAdd;
        public TarkovMarketItem SelectedItemToAdd
        {
            get => _selectedItemToAdd;
            set { if (_selectedItemToAdd != value) { _selectedItemToAdd = value; OnPropertyChanged(); } }
        }

        private void EnsureFirstItemSelected()
        {
            var first = FilteredItems.Cast<TarkovMarketItem>().FirstOrDefault();
            SelectedItemToAdd = first;
        }

        private string _itemSearchText;
        public string ItemSearchText
        {
            get => _itemSearchText;
            set
            {
                if (_itemSearchText == value) return;
                _itemSearchText = value;
                OnPropertyChanged();
                _filteredItems.Refresh();
                EnsureFirstItemSelected();
            }
        }

        public ICommand OpenItemSelectorCommand { get; }
        private void OnOpenItemSelector()
        {
            var viewModel = new ItemSelectorViewModel(AvailableItems);
            var window = new ItemSelectorWindow(viewModel) { Owner = MainWindow.Instance };

            if (window.ShowDialog() == true && viewModel.SelectedItem != null)
            {
                var userFilter = App.Config.LootFilters.Filters[SelectedFilterName];
                var entry = new LootFilterEntry
                {
                    ItemID = viewModel.SelectedItem.BsgId,
                    ParentFilter = userFilter,
                    ExplicitColor = GetValidFilterColor()
                };
                Entries.Add(entry);
            }
        }

        public ICommand AddEntryCommand { get; }
        private void OnAddEntry()
        {
            if (SelectedItemToAdd == null) return;

            var userFilter = App.Config.LootFilters.Filters[SelectedFilterName];
            var entry = new LootFilterEntry
            {
                ItemID = SelectedItemToAdd.BsgId,
                ParentFilter = userFilter,
                ExplicitColor = GetValidFilterColor()
            };

            Entries.Add(entry);
            SelectedItemToAdd = null;
        }

        public ICommand RemoveEntryCommand { get; }
        private void OnRemoveEntry(object o)
        {
            if (o is LootFilterEntry entry && Entries.Contains(entry))
            {
                Entries.Remove(entry);
                LootFilterManager.RefreshLootFilter();
            }
        }

        public ICommand DeleteEntryCommand { get; }
        private void OnDeleteEntry() { }

        public ICommand ApplyColorToAllCommand { get; }
        private void OnApplyColorToAll()
        {
            if (string.IsNullOrEmpty(SelectedFilterName) || Entries == null || Entries.Count == 0)
                return;

            string colorToApply = GetValidFilterColor();
            foreach (var entry in Entries)
                entry.ExplicitColor = colorToApply;
        }

        public ICommand EnableAllEntriesCommand { get; }
        private void OnEnableAllEntries()
        {
            if (string.IsNullOrEmpty(SelectedFilterName) || Entries == null || Entries.Count == 0)
                return;

            bool allEnabled = Entries.All(e => e.Enabled);
            foreach (var entry in Entries)
                entry.Enabled = !allEnabled;
            
            OnPropertyChanged(nameof(ToggleAllButtonText));
        }

        public string ToggleAllButtonText
        {
            get
            {
                if (Entries == null || Entries.Count == 0)
                    return "Toggle All";
                return Entries.All(e => e.Enabled) ? "Disable All" : "Enable All";
            }
        }

        public void DeleteEntry(LootFilterEntry entry)
        {
            if (entry == null) return;
            if (Entries.Contains(entry))
            {
                Entries.Remove(entry);
                LootFilterManager.RefreshLootFilter();
            }
        }

        public IEnumerable<LootFilterEntryType> FilterEntryTypes { get; } = 
            Enum.GetValues<LootFilterEntryType>().Cast<LootFilterEntryType>();

        private ObservableCollection<LootFilterEntry> _entries = new();
        public ObservableCollection<LootFilterEntry> Entries
        {
            get => _entries;
            set
            {
                if (_entries != value)
                {
                    _entries = value;
                    _cachedMatchingEntries = null;
                    _cachedSearchText = null;
                    UpdateFilteredEntriesView();
                    OnPropertyChanged(nameof(Entries));
                }
            }
        }

        private ICollectionView _filteredEntriesView;
        public ICollectionView FilteredEntries
        {
            get
            {
                if (_filteredEntriesView == null && _entries != null)
                {
                    _filteredEntriesView = CollectionViewSource.GetDefaultView(_entries);
                    _filteredEntriesView.Filter = EntryFilterPredicate;
                }
                return _filteredEntriesView;
            }
        }

        private int _maxDisplayItems = 50;
        public int MaxDisplayItems
        {
            get => _maxDisplayItems;
            set
            {
                if (_maxDisplayItems != value)
                {
                    _maxDisplayItems = Math.Max(1, value);
                    OnPropertyChanged();
                    _filteredEntriesView?.Refresh();
                    OnPropertyChanged(nameof(EntryCountText));
                }
            }
        }

        private bool _showAllEntries = false;
        public bool ShowAllEntries
        {
            get => _showAllEntries;
            set
            {
                if (_showAllEntries != value)
                {
                    _showAllEntries = value;
                    OnPropertyChanged();
                    _filteredEntriesView?.Refresh();
                    OnPropertyChanged(nameof(EntryCountText));
                }
            }
        }

        private string _entrySearchText = string.Empty;
        public string EntrySearchText
        {
            get => _entrySearchText;
            set
            {
                if (_entrySearchText != value)
                {
                    _entrySearchText = value;
                    OnPropertyChanged();
                    _filteredEntriesView?.Refresh();
                    OnPropertyChanged(nameof(EntryCountText));
                }
            }
        }

        public string EntryCountText
        {
            get
            {
                if (_entries == null || _entries.Count == 0)
                    return "No entries";
                
                int total = _entries.Count;
                int filtered = _filteredEntriesView?.Cast<LootFilterEntry>().Count() ?? total;
                
                return filtered == total ? $"{total} entry/entries" : $"{filtered} of {total} entries";
            }
        }

        private void UpdateFilteredEntriesView()
        {
            _cachedMatchingEntries = null;
            _cachedSearchText = null;
            
            if (_entries != null)
            {
                _filteredEntriesView = CollectionViewSource.GetDefaultView(_entries);
                _filteredEntriesView.Filter = EntryFilterPredicate;
            }
            else
            {
                _filteredEntriesView = null;
            }
            OnPropertyChanged(nameof(FilteredEntries));
            OnPropertyChanged(nameof(EntryCountText));
        }

#nullable enable
        private List<LootFilterEntry>? _cachedMatchingEntries = null;
        private string? _cachedSearchText = null;
#nullable restore

        private bool EntryFilterPredicate(object obj)
        {
            if (obj is not LootFilterEntry entry)
                return false;

            if (!string.IsNullOrWhiteSpace(_entrySearchText))
            {
                string search = _entrySearchText.ToLowerInvariant();
                bool matchesSearch = entry.ItemID.ToLowerInvariant().Contains(search) ||
                                   entry.Name.ToLowerInvariant().Contains(search) ||
                                   (entry.Comment?.ToLowerInvariant().Contains(search) ?? false);
                
                if (!matchesSearch)
                    return false;

                if (_cachedSearchText != _entrySearchText || _cachedMatchingEntries == null)
                {
                    _cachedMatchingEntries = _entries
                        .Where(e => e.ItemID.ToLowerInvariant().Contains(search) ||
                                   e.Name.ToLowerInvariant().Contains(search) ||
                                   (e.Comment?.ToLowerInvariant().Contains(search) ?? false))
                        .ToList();
                    _cachedSearchText = _entrySearchText;
                }

                if (!_showAllEntries)
                {
                    int index = _cachedMatchingEntries.IndexOf(entry);
                    if (index >= MaxDisplayItems)
                        return false;
                }

                return true;
            }
            else
            {
                _cachedMatchingEntries = null;
                _cachedSearchText = null;

                if (!_showAllEntries)
                {
                    int index = _entries.IndexOf(entry);
                    if (index >= MaxDisplayItems)
                        return false;
                }

                return true;
            }
        }

        #endregion

        #region Misc

        private string GetValidFilterColor()
        {
            if (!string.IsNullOrWhiteSpace(CurrentFilterColor))
                return CurrentFilterColor;

            if (!string.IsNullOrEmpty(SelectedFilterName) &&
                App.Config.LootFilters.Filters.TryGetValue(SelectedFilterName, out var userFilter) &&
                !string.IsNullOrWhiteSpace(userFilter.Color))
                return userFilter.Color;

            return SKColors.Turquoise.ToString();
        }

        private bool FilterPredicate(object obj)
        {
            if (string.IsNullOrWhiteSpace(_itemSearchText))
                return true;

            var itm = obj as TarkovMarketItem;
            return itm?.Name.IndexOf(_itemSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion
    }
}
