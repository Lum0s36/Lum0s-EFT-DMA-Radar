/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Tarkov;
using LoneEftDmaRadar.UI.Misc;
using System.Collections.ObjectModel;
using System.Windows;

namespace LoneEftDmaRadar.UI.Loot.Helpers
{
    /// <summary>
    /// Handles filter CRUD operations (Create, Rename, Delete).
    /// </summary>
    public sealed class LootFilterManager
    {
        private readonly ObservableCollection<string> _filterNames;
        private readonly Action<string> _onFilterSelected;

        public LootFilterManager(ObservableCollection<string> filterNames, Action<string> onFilterSelected)
        {
            _filterNames = filterNames ?? throw new ArgumentNullException(nameof(filterNames));
            _onFilterSelected = onFilterSelected ?? throw new ArgumentNullException(nameof(onFilterSelected));
        }

        /// <summary>
        /// Creates a new filter with user-provided name.
        /// </summary>
        public bool AddFilter()
        {
            var dlg = new InputBoxWindow("Loot Filter", "Enter the name of the new loot filter:");
            if (dlg.ShowDialog() != true)
                return false;

            var name = dlg.InputText;
            if (string.IsNullOrEmpty(name))
                return false;

            try
            {
                if (!App.Config.LootFilters.Filters.TryAdd(name, new UserLootFilter
                {
                    Enabled = true,
                    Entries = new()
                }))
                {
                    throw new InvalidOperationException("That filter already exists.");
                }

                _filterNames.Add(name);
                _onFilterSelected(name);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    MainWindow.Instance,
                    $"ERROR Adding Filter: {ex.Message}",
                    "Loot Filter",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Renames the currently selected filter.
        /// </summary>
        public bool RenameFilter(string oldName)
        {
            if (string.IsNullOrEmpty(oldName))
                return false;

            var dlg = new InputBoxWindow($"Rename {oldName}", "Enter the new filter name:");
            if (dlg.ShowDialog() != true)
                return false;

            var newName = dlg.InputText;
            if (string.IsNullOrEmpty(newName))
                return false;

            try
            {
                if (App.Config.LootFilters.Filters.TryGetValue(oldName, out var filter)
                    && App.Config.LootFilters.Filters.TryAdd(newName, filter)
                    && App.Config.LootFilters.Filters.TryRemove(oldName, out _))
                {
                    var idx = _filterNames.IndexOf(oldName);
                    _filterNames[idx] = newName;
                    _onFilterSelected(newName);
                    return true;
                }
                else
                {
                    throw new InvalidOperationException("Rename failed.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    MainWindow.Instance,
                    $"ERROR Renaming Filter: {ex.Message}",
                    "Loot Filter",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Deletes the currently selected filter.
        /// </summary>
        public bool DeleteFilter(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(
                    MainWindow.Instance,
                    "No loot filter selected!",
                    "Loot Filter",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            var result = MessageBox.Show(
                MainWindow.Instance,
                $"Are you sure you want to delete '{name}'?",
                "Loot Filter",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return false;

            try
            {
                if (!App.Config.LootFilters.Filters.TryRemove(name, out _))
                    throw new InvalidOperationException("Remove failed.");

                // Ensure at least one filter remains
                if (App.Config.LootFilters.Filters.IsEmpty)
                {
                    App.Config.LootFilters.Filters.TryAdd("default", new UserLootFilter
                    {
                        Enabled = true,
                        Entries = new()
                    });
                }

                _filterNames.Clear();
                foreach (var key in App.Config.LootFilters.Filters.Keys)
                    _filterNames.Add(key);

                _onFilterSelected(_filterNames[0]);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    MainWindow.Instance,
                    $"ERROR Deleting Filter: {ex.Message}",
                    "Loot Filter",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Refreshes the loot filter on all items.
        /// </summary>
        public static void RefreshLootFilter()
        {
            // Remove old filters
            foreach (var item in TarkovDataManager.AllItems.Values)
                item.SetFilter(null);

            // Set new filters
            var currentFilters = App.Config.LootFilters.Filters
                .Values
                .Where(x => x.Enabled)
                .SelectMany(x => x.Entries);

            if (!currentFilters.Any())
                return;

            foreach (var filter in currentFilters)
            {
                if (string.IsNullOrEmpty(filter.ItemID))
                    continue;
                if (TarkovDataManager.AllItems.TryGetValue(filter.ItemID, out var item))
                    item.SetFilter(filter);
            }
        }
    }
}
