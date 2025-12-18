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

using LoneEftDmaRadar.Tarkov;
using LoneEftDmaRadar.UI.Loot;
using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.UI.Radar.Views;
using LoneEftDmaRadar.Web.TarkovDev.Data;
using LoneEftDmaRadar;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Input;
using SkiaSharp;
using Microsoft.Win32;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Text;
using System.Windows;
using System.IO.Compression;

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

        public LootFiltersViewModel(LootFiltersTab parent)
        {
            FilterNames = new ObservableCollection<string>(App.Config.LootFilters.Filters.Keys);
            AvailableItems = new ObservableCollection<TarkovMarketItem>(
                TarkovDataManager.AllItems.Values.OrderBy(x => x.Name));

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
            RefreshLootFilter();
            parent.IsVisibleChanged += Parent_IsVisibleChanged;
        }

        private void Parent_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool visible && !visible)
            {
                RefreshLootFilter();
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
                // persist to config
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
                // persist to config
                App.Config.LootFilters.Filters[SelectedFilterName].Color = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> FilterNames { get; } // ComboBox of filter names
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
                // Assign parent filter reference to each entry
                foreach (var entry in userFilter.Entries)
                    entry.ParentFilter = userFilter;
                // Clear search when switching filters
                EntrySearchText = string.Empty;
                // Reset show all when switching filters
                ShowAllEntries = false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EntryCountText));
                OnPropertyChanged(nameof(ToggleAllButtonText));
            }
        }

        public ICommand AddFilterCommand { get; }
        private void OnAddFilter()
        {
            var dlg = new InputBoxWindow("Loot Filter", "Enter the name of the new loot filter:");
            if (dlg.ShowDialog() != true)
                return; // user cancelled
            var name = dlg.InputText;
            if (string.IsNullOrEmpty(name)) return;

            try
            {
                if (!App.Config.LootFilters.Filters.TryAdd(name, new UserLootFilter
                {
                    Enabled = true,
                    Entries = new()
                }))
                    throw new InvalidOperationException("That filter already exists.");

                FilterNames.Add(name);
                SelectedFilterName = name;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    MainWindow.Instance,
                    $"ERROR Adding Filter: {ex.Message}",
                    "Loot Filter",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public ICommand RenameFilterCommand { get; }
        private void OnRenameFilter()
        {
            var oldName = SelectedFilterName;
            if (string.IsNullOrEmpty(oldName)) return;

            var dlg = new InputBoxWindow($"Rename {oldName}", "Enter the new filter name:");
            if (dlg.ShowDialog() != true)
                return; // user cancelled
            var newName = dlg.InputText;
            if (string.IsNullOrEmpty(newName)) return;

            try
            {
                if (App.Config.LootFilters.Filters.TryGetValue(oldName, out var filter)
                    && App.Config.LootFilters.Filters.TryAdd(newName, filter)
                    && App.Config.LootFilters.Filters.TryRemove(oldName, out _))
                {
                    var idx = FilterNames.IndexOf(oldName);
                    FilterNames[idx] = newName;
                    SelectedFilterName = newName;
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
            }
        }

        public ICommand DeleteFilterCommand { get; }
        private void OnDeleteFilter()
        {
            var name = SelectedFilterName;
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(
                    MainWindow.Instance,
                    "No loot filter selected!",
                    "Loot Filter",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                MainWindow.Instance,
                $"Are you sure you want to delete '{name}'?",
                "Loot Filter",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (!App.Config.LootFilters.Filters.TryRemove(name, out _))
                    throw new InvalidOperationException("Remove failed.");

                // ensure at least one filter remains
                if (App.Config.LootFilters.Filters.IsEmpty)
                    App.Config.LootFilters.Filters.TryAdd("default", new UserLootFilter
                    {
                        Enabled = true,
                        Entries = new()
                    });

                FilterNames.Clear();
                foreach (var key in App.Config.LootFilters.Filters.Keys)
                    FilterNames.Add(key);

                SelectedFilterName = FilterNames[0];
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    MainWindow.Instance,
                    $"ERROR Deleting Filter: {ex.Message}",
                    "Loot Filter",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public ICommand ExportFiltersCommand { get; }
        private void OnExportFilters()
        {
            try
            {
                // Ask user which export method to use
                var exportMethod = MessageBox.Show(
                    MainWindow.Instance,
                    "Choose export method:\n\n" +
                    "Yes = Export to File (JSON)\n" +
                    "No = Export as Compact Text (Base64)\n" +
                    "Cancel = Abort",
                    "Export Loot Filters",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (exportMethod == MessageBoxResult.Cancel)
                    return;

                // Create export data structure (stable format, no ConcurrentDictionary)
                var exportData = new LootFiltersExportData
                {
                    Version = "1.0",
                    ExportDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    Filters = App.Config.LootFilters.Filters.Select(kvp => new FilterExportItem
                    {
                        Name = kvp.Key,
                        Enabled = kvp.Value.Enabled,
                        Color = kvp.Value.Color,
                        Entries = kvp.Value.Entries.Select(e => new EntryExportItem
                        {
                            ItemID = e.ItemID,
                            Enabled = e.Enabled,
                            Type = e.Type,
                            Comment = e.Comment,
                            Color = e.ExplicitColor
                        }).ToList()
                    }).ToList()
                };

                if (exportMethod == MessageBoxResult.Yes)
                {
                    // Export to File
                    var dialog = new SaveFileDialog
                    {
                        Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                        DefaultExt = "json",
                        FileName = "loot_filters_export.json",
                        Title = "Export Loot Filters to File"
                    };

                    if (dialog.ShowDialog() != true)
                        return;

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    var json = JsonSerializer.Serialize(exportData, options);
                    File.WriteAllText(dialog.FileName, json);

                    MessageBox.Show(
                        MainWindow.Instance,
                        $"Successfully exported {exportData.Filters.Count} filter(s) to:\n{dialog.FileName}",
                        "Export Loot Filters",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    // Export as Compact Text (Base64 + GZip compressed)
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = false, // Minified
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    var json = JsonSerializer.Serialize(exportData, options);
                    var jsonBytes = Encoding.UTF8.GetBytes(json);
                    
                    // Compress with GZip
                    byte[] compressedBytes;
                    using (var memoryStream = new MemoryStream())
                    {
                        using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
                        {
                            gzipStream.Write(jsonBytes, 0, jsonBytes.Length);
                        }
                        compressedBytes = memoryStream.ToArray();
                    }
                    
                    var base64 = Convert.ToBase64String(compressedBytes);

                    // Show result dialog with text
                    var resultWindow = new Window
                    {
                        Title = "Export Loot Filters - Compact Text",
                        Width = 700,
                        Height = 500,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = MainWindow.Instance,
                        ResizeMode = ResizeMode.CanResize
                    };

                    var grid = new System.Windows.Controls.Grid
                    {
                        Margin = new Thickness(10)
                    };
                    grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
                    grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
                    grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

                    var textBox = new System.Windows.Controls.TextBox
                    {
                        Text = base64,
                        IsReadOnly = false, // Allow user to select and copy
                        VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                        TextWrapping = TextWrapping.NoWrap,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize = 11,
                        Margin = new Thickness(0, 10, 0, 0)
                    };
                    System.Windows.Controls.Grid.SetRow(textBox, 1);

                    var originalSize = jsonBytes.Length;
                    var compressedSize = compressedBytes.Length;
                    var compressionRatio = originalSize > 0 ? (1.0 - (double)compressedSize / originalSize) * 100 : 0;

                    var infoText = new System.Windows.Controls.TextBlock
                    {
                        Text = $"Exported {exportData.Filters.Count} filter(s) as compressed Base64 text.\n" +
                               $"Select the text below and copy it manually (Ctrl+C).\n\n" +
                               $"Length: {base64.Length} characters\n" +
                               $"Compression: {compressionRatio:F1}% smaller",
                        TextWrapping = TextWrapping.Wrap
                    };
                    System.Windows.Controls.Grid.SetRow(infoText, 0);

                    var closeButton = new System.Windows.Controls.Button
                    {
                        Content = "Close",
                        Width = 100,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 10, 0, 0)
                    };
                    closeButton.Click += (s, e) => resultWindow.Close();
                    System.Windows.Controls.Grid.SetRow(closeButton, 2);

                    grid.Children.Add(infoText);
                    grid.Children.Add(textBox);
                    grid.Children.Add(closeButton);

                    resultWindow.Content = grid;
                    resultWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    MainWindow.Instance,
                    $"ERROR Exporting Filters: {ex.Message}",
                    "Export Loot Filters",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public ICommand ImportFiltersCommand { get; }
        private void OnImportFilters()
        {
            try
            {
                // Ask user which import method to use
                var importMethod = MessageBox.Show(
                    MainWindow.Instance,
                    "Choose import method:\n\n" +
                    "Yes = Import from File (JSON)\n" +
                    "No = Import from Compact Text (Base64)\n" +
                    "Cancel = Abort",
                    "Import Loot Filters",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (importMethod == MessageBoxResult.Cancel)
                    return;

                string json;

                if (importMethod == MessageBoxResult.Yes)
                {
                    // Import from File
                    var dialog = new OpenFileDialog
                    {
                        Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                        DefaultExt = "json",
                        Title = "Import Loot Filters from File"
                    };

                    if (dialog.ShowDialog() != true)
                        return;

                    json = File.ReadAllText(dialog.FileName);
                }
                else
                {
                    // Import from Compact Text (Base64)
                    var inputWindow = new Window
                    {
                        Title = "Import Loot Filters - Compact Text",
                        Width = 600,
                        Height = 400,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = MainWindow.Instance,
                        ResizeMode = ResizeMode.CanResize
                    };

                    var textBox = new System.Windows.Controls.TextBox
                    {
                        AcceptsReturn = true,
                        VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                        TextWrapping = TextWrapping.NoWrap,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize = 11,
                        Margin = new Thickness(10, 10, 10, 0)
                    };

                    var stackPanel = new System.Windows.Controls.StackPanel
                    {
                        Margin = new Thickness(10)
                    };

                    var infoText = new System.Windows.Controls.TextBlock
                    {
                        Text = "Paste the Base64 encoded text below:",
                        Margin = new Thickness(0, 0, 0, 10)
                    };

                    var buttonPanel = new System.Windows.Controls.StackPanel
                    {
                        Orientation = System.Windows.Controls.Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 10, 0, 0)
                    };

                    var okButton = new System.Windows.Controls.Button
                    {
                        Content = "Import",
                        Width = 100,
                        Margin = new Thickness(0, 0, 10, 0),
                        IsDefault = true
                    };

                    var cancelButton = new System.Windows.Controls.Button
                    {
                        Content = "Cancel",
                        Width = 100,
                        IsCancel = true
                    };

                    bool importConfirmed = false;
                    okButton.Click += (s, e) =>
                    {
                        importConfirmed = true;
                        inputWindow.Close();
                    };
                    cancelButton.Click += (s, e) => inputWindow.Close();

                    buttonPanel.Children.Add(okButton);
                    buttonPanel.Children.Add(cancelButton);

                    stackPanel.Children.Add(infoText);
                    stackPanel.Children.Add(textBox);
                    stackPanel.Children.Add(buttonPanel);

                    inputWindow.Content = stackPanel;
                    inputWindow.ShowDialog();

                    if (!importConfirmed || string.IsNullOrWhiteSpace(textBox.Text))
                        return;

                    try
                    {
                        var base64Text = textBox.Text.Trim();
                        var compressedBytes = Convert.FromBase64String(base64Text);
                        
                        // Try to decompress (GZip)
                        try
                        {
                            using (var memoryStream = new MemoryStream(compressedBytes))
                            using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                            using (var reader = new StreamReader(gzipStream, Encoding.UTF8))
                            {
                                json = reader.ReadToEnd();
                            }
                        }
                        catch
                        {
                            // If decompression fails, try as plain Base64 (backward compatibility)
                            var jsonBytes = compressedBytes;
                            json = Encoding.UTF8.GetString(jsonBytes);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            MainWindow.Instance,
                            $"Invalid Base64 text: {ex.Message}",
                            "Import Loot Filters",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }
                }
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var importData = JsonSerializer.Deserialize<LootFiltersExportData>(json, options);
                if (importData == null)
                {
                    MessageBox.Show(
                        MainWindow.Instance,
                        "Invalid or empty import file.",
                        "Import Loot Filters",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Convert new format (lootFilters wrapper) to old format (array)
                List<FilterExportItem> filtersToImport = new();
                
                if (importData.LootFilters != null && importData.LootFilters.Filters != null)
                {
                    // New format: lootFilters.filters is a dictionary
                    foreach (var kvp in importData.LootFilters.Filters)
                    {
                        var filterObj = kvp.Value;
                        filtersToImport.Add(new FilterExportItem
                        {
                            Name = kvp.Key,
                            Enabled = filterObj.Enabled,
                            Entries = filterObj.Entries ?? new List<EntryExportItem>()
                        });
                    }
                    
                    // Set selected filter if specified
                    if (!string.IsNullOrEmpty(importData.LootFilters.Selected))
                    {
                        // Note: This is informational, actual selection happens later
                    }
                }
                else if (importData.Filters != null && importData.Filters.Any())
                {
                    // Old format: filters is an array
                    filtersToImport = importData.Filters;
                }
                else
                {
                    MessageBox.Show(
                        MainWindow.Instance,
                        "Invalid or empty import file.",
                        "Import Loot Filters",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!filtersToImport.Any())
                {
                    MessageBox.Show(
                        MainWindow.Instance,
                        "No filters found in import file.",
                        "Import Loot Filters",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Check if a filter is selected
                if (string.IsNullOrEmpty(SelectedFilterName))
                {
                    MessageBox.Show(
                        MainWindow.Instance,
                        "Please select a filter first, or choose 'Yes' to create new filters.",
                        "Import Loot Filters",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                // Ask user how to import
                var result = MessageBox.Show(
                    MainWindow.Instance,
                    $"Found {filtersToImport.Count} filter(s) to import.\n\n" +
                    "How would you like to import?\n\n" +
                    "Yes = Create new filters (if name exists, will add suffix)\n" +
                    "No = Replace the currently selected filter\n" +
                    "Cancel = Abort import",
                    "Import Loot Filters",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                    return;

                if (result == MessageBoxResult.No)
                {
                    // Replace currently selected filter
                    if (string.IsNullOrEmpty(SelectedFilterName))
                    {
                        MessageBox.Show(
                            MainWindow.Instance,
                            "No filter selected. Please select a filter first.",
                            "Import Loot Filters",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    // Merge all imported filters into the selected filter
                    var selectedFilter = App.Config.LootFilters.Filters[SelectedFilterName];
                    selectedFilter.Entries.Clear();

                    int totalEntries = 0;
                    foreach (var filterData in filtersToImport)
                    {
                        if (filterData.Entries != null)
                        {
                            foreach (var entryData in filterData.Entries)
                            {
                                if (string.IsNullOrWhiteSpace(entryData.ItemID))
                                    continue;

                                // Entry type is already parsed by EntryTypeConverter
                                var entry = new LootFilterEntry
                                {
                                    ItemID = entryData.ItemID,
                                    Enabled = entryData.Enabled,
                                    Type = entryData.Type,
                                    Comment = entryData.Comment ?? string.Empty,
                                    ExplicitColor = entryData.Color,
                                    ParentFilter = selectedFilter
                                };

                                selectedFilter.Entries.Add(entry);
                                totalEntries++;
                            }
                        }
                    }

                    // Update filter properties from first imported filter (if any)
                    if (filtersToImport.Any())
                    {
                        var firstFilter = filtersToImport.First();
                        selectedFilter.Enabled = firstFilter.Enabled;
                        selectedFilter.Color = firstFilter.Color ?? selectedFilter.Color;
                    }

                    // Refresh UI
                    CurrentFilterEnabled = selectedFilter.Enabled;
                    CurrentFilterColor = selectedFilter.Color;
                    Entries = selectedFilter.Entries;
                    foreach (var entry in selectedFilter.Entries)
                        entry.ParentFilter = selectedFilter;

                    RefreshLootFilter();

                    MessageBox.Show(
                        MainWindow.Instance,
                        $"Successfully imported {totalEntries} entry/entries into '{SelectedFilterName}' filter.",
                        "Import Loot Filters",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    // Create new filters
                    int imported = 0;
                    int skipped = 0;

                    foreach (var filterData in filtersToImport)
                    {
                        if (string.IsNullOrWhiteSpace(filterData.Name))
                            continue;

                        // Generate unique name if filter already exists
                        string filterName = filterData.Name;
                        int suffix = 1;
                        while (App.Config.LootFilters.Filters.ContainsKey(filterName))
                        {
                            filterName = $"{filterData.Name} ({suffix})";
                            suffix++;
                        }

                        // Create new filter
                        var newFilter = new UserLootFilter
                        {
                            Enabled = filterData.Enabled,
                            Color = filterData.Color ?? SKColors.Turquoise.ToString()
                        };

                        // Import entries
                        if (filterData.Entries != null)
                        {
                            foreach (var entryData in filterData.Entries)
                            {
                                if (string.IsNullOrWhiteSpace(entryData.ItemID))
                                    continue;

                                // Entry type is already parsed by EntryTypeConverter
                                var entry = new LootFilterEntry
                                {
                                    ItemID = entryData.ItemID,
                                    Enabled = entryData.Enabled,
                                    Type = entryData.Type,
                                    Comment = entryData.Comment ?? string.Empty,
                                    ExplicitColor = entryData.Color,
                                    ParentFilter = newFilter
                                };

                                newFilter.Entries.Add(entry);
                            }
                        }

                        App.Config.LootFilters.Filters.TryAdd(filterName, newFilter);
                        FilterNames.Add(filterName);
                        imported++;

                        // If this was the original name that was changed, note it
                        if (filterName != filterData.Name)
                            skipped++;
                    }

                    // Refresh UI
                    if (imported > 0)
                    {
                        // Select the first imported filter
                        if (filtersToImport.Any())
                        {
                            string firstFilterName = filtersToImport.First().Name;
                            // Find the actual name (might have suffix)
                            string actualName = FilterNames.FirstOrDefault(n => n == firstFilterName || n.StartsWith(firstFilterName + " ("));
                            if (actualName != null)
                            {
                                SelectedFilterName = actualName;
                            }
                        }

                        RefreshLootFilter();
                    }

                    string message = $"Import completed:\n• Created: {imported} new filter(s)";
                    if (skipped > 0)
                        message += $"\n• Renamed: {skipped} filter(s) (name conflict)";

                    MessageBox.Show(
                        MainWindow.Instance,
                        message,
                        "Import Loot Filters",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    MainWindow.Instance,
                    $"ERROR Importing Filters: {ex.Message}",
                    "Import Loot Filters",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region Bottom Section - Entries

        public ObservableCollection<TarkovMarketItem> AvailableItems { get; } // List of items
        private ICollectionView _filteredItems;
        public ICollectionView FilteredItems // Filtered list of items
        {
            get
            {
                if (_filteredItems == null)
                {
                    // create the view once
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
                _filteredItems.Refresh(); // refresh the filter
                EnsureFirstItemSelected();
            }
        }

        public ICommand OpenItemSelectorCommand { get; }
        private void OnOpenItemSelector()
        {
            var viewModel = new ItemSelectorViewModel(AvailableItems);
            var window = new ItemSelectorWindow(viewModel)
            {
                Owner = MainWindow.Instance
            };

            if (window.ShowDialog() == true && viewModel.SelectedItem != null)
            {
                // Add item directly to filter
                var userFilter = App.Config.LootFilters.Filters[SelectedFilterName];
                
                // Get color with validation and fallback
                string colorToApply = GetValidFilterColor();
                
                var entry = new LootFilterEntry
                {
                    ItemID = viewModel.SelectedItem.BsgId,
                    ParentFilter = userFilter,
                    ExplicitColor = colorToApply
                };

                Entries.Add(entry);
            }
        }

        public ICommand AddEntryCommand { get; }
        private void OnAddEntry()
        {
            if (SelectedItemToAdd == null) return;

            var userFilter = App.Config.LootFilters.Filters[SelectedFilterName];
            
            // Get color with validation and fallback
            string colorToApply = GetValidFilterColor();
            
            var entry = new LootFilterEntry
            {
                ItemID = SelectedItemToAdd.BsgId,
                ParentFilter = userFilter,
                ExplicitColor = colorToApply
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
                RefreshLootFilter();
            }
        }

        public ICommand DeleteEntryCommand { get; }
        private void OnDeleteEntry()
        {
            // Invoked via context menu; selection handled in view's code-behind
        }

        public ICommand ApplyColorToAllCommand { get; }
        private void OnApplyColorToAll()
        {
            if (string.IsNullOrEmpty(SelectedFilterName) || Entries == null || Entries.Count == 0)
                return;

            // Get color with validation and fallback
            string colorToApply = GetValidFilterColor();

            // Apply color to all entries
            foreach (var entry in Entries)
            {
                entry.ExplicitColor = colorToApply;
            }
        }

        public ICommand EnableAllEntriesCommand { get; }
        private void OnEnableAllEntries()
        {
            if (string.IsNullOrEmpty(SelectedFilterName) || Entries == null || Entries.Count == 0)
                return;

            // Check if all entries are enabled
            bool allEnabled = Entries.All(e => e.Enabled);

            // Toggle: if all enabled -> disable all, else enable all
            foreach (var entry in Entries)
            {
                entry.Enabled = !allEnabled;
            }
            
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
                RefreshLootFilter();
            }
        }

        public IEnumerable<LootFilterEntryType> FilterEntryTypes { get; } = Enum // ComboBox of Entry Types within DataGrid
            .GetValues<LootFilterEntryType>()
            .Cast<LootFilterEntryType>();

        private ObservableCollection<LootFilterEntry> _entries = new();
        public ObservableCollection<LootFilterEntry> Entries // Entries grid
        {
            get => _entries;
            set
            {
                if (_entries != value)
                {
                    _entries = value;
                    // Clear cache when entries change
                    _cachedMatchingEntries = null;
                    _cachedSearchText = null;
                    // Update filtered view when entries change
                    UpdateFilteredEntriesView();
                    OnPropertyChanged(nameof(Entries));
                }
            }
        }

        private ICollectionView _filteredEntriesView;
        public ICollectionView FilteredEntries // Filtered and limited entries for DataGrid
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
                    // Ensure minimum value of 1
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
                
                if (filtered == total)
                    return $"{total} entry/entries";
                else
                    return $"{filtered} of {total} entries";
            }
        }

        private void UpdateFilteredEntriesView()
        {
            // Clear cache when updating view
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
        // Cache for matching items when searching (to avoid repeated calculations)
        private List<LootFilterEntry>? _cachedMatchingEntries = null;
        private string? _cachedSearchText = null;
#nullable restore

        private bool EntryFilterPredicate(object obj)
        {
            if (obj is not LootFilterEntry entry)
                return false;

            // Apply search filter first
            if (!string.IsNullOrWhiteSpace(_entrySearchText))
            {
                string search = _entrySearchText.ToLowerInvariant();
                bool matchesSearch = entry.ItemID.ToLowerInvariant().Contains(search) ||
                                   entry.Name.ToLowerInvariant().Contains(search) ||
                                   (entry.Comment?.ToLowerInvariant().Contains(search) ?? false);
                
                if (!matchesSearch)
                    return false;

                // When searching, rebuild cache if search text changed
                if (_cachedSearchText != _entrySearchText || _cachedMatchingEntries == null)
                {
                    _cachedMatchingEntries = _entries
                        .Where(e => e.ItemID.ToLowerInvariant().Contains(search) ||
                                   e.Name.ToLowerInvariant().Contains(search) ||
                                   (e.Comment?.ToLowerInvariant().Contains(search) ?? false))
                        .ToList();
                    _cachedSearchText = _entrySearchText;
                }

                // Apply limit if not showing all
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
                // Clear cache when no search
                _cachedMatchingEntries = null;
                _cachedSearchText = null;

                // Apply limit if not showing all
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

        /// <summary>
        /// Gets a valid filter color with fallback logic.
        /// </summary>
        private string GetValidFilterColor()
        {
            // First try CurrentFilterColor
            if (!string.IsNullOrWhiteSpace(CurrentFilterColor))
                return CurrentFilterColor;

            // Fallback to filter's stored color
            if (!string.IsNullOrEmpty(SelectedFilterName) &&
                App.Config.LootFilters.Filters.TryGetValue(SelectedFilterName, out var userFilter) &&
                !string.IsNullOrWhiteSpace(userFilter.Color))
                return userFilter.Color;

            // Final fallback to default
            return SKColors.Turquoise.ToString();
        }

        private bool FilterPredicate(object obj)
        {
            if (string.IsNullOrWhiteSpace(_itemSearchText))
                return true;

            var itm = obj as TarkovMarketItem;
            return itm?.Name
                       .IndexOf(_itemSearchText,
                                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Refreshes the Loot Filter.
        /// Should be called at startup and during validation.
        /// </summary>
        private static void RefreshLootFilter()
        {
            /// Remove old filters (if any)
            foreach (var item in TarkovDataManager.AllItems.Values)
                item.SetFilter(null);
            /// Set new filters
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

        #endregion
    }

    #region Export/Import Data Structures

#nullable enable
    /// <summary>
    /// Stable export format for Loot Filters (does not use ConcurrentDictionary)
    /// Supports both old format (array) and new format (object with lootFilters wrapper)
    /// </summary>
    internal sealed class LootFiltersExportData
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("exportDate")]
        public string? ExportDate { get; set; }

        [JsonPropertyName("filters")]
        public List<FilterExportItem>? Filters { get; set; }

        // New format support
        [JsonPropertyName("lootFilters")]
        public LootFiltersWrapper? LootFilters { get; set; }
    }

    /// <summary>
    /// Wrapper for new JSON format with lootFilters object
    /// </summary>
    internal sealed class LootFiltersWrapper
    {
        [JsonPropertyName("selected")]
        public string? Selected { get; set; }

        [JsonPropertyName("filters")]
        public Dictionary<string, FilterExportItemObject>? Filters { get; set; }
    }

    /// <summary>
    /// Filter item for new format (object-based, no name field)
    /// </summary>
    internal sealed class FilterExportItemObject
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("entries")]
        public List<EntryExportItem> Entries { get; set; } = new();
    }

    /// <summary>
    /// Filter item for old format (array-based, with name field)
    /// </summary>
    internal sealed class FilterExportItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("color")]
        public string? Color { get; set; }

        [JsonPropertyName("entries")]
        public List<EntryExportItem> Entries { get; set; } = new();
    }

    /// <summary>
    /// Entry item that supports both string and number type formats
    /// </summary>
    internal sealed class EntryExportItem
    {
        [JsonPropertyName("itemID")]
        public string ItemID { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        // Support both string and number types
        [JsonPropertyName("type")]
        [JsonConverter(typeof(EntryTypeConverter))]
        public LootFilterEntryType Type { get; set; } = LootFilterEntryType.ImportantLoot;

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonPropertyName("color")]
        public string? Color { get; set; }
    }

    /// <summary>
    /// Custom converter for EntryType that supports both string and number formats
    /// </summary>
    internal sealed class EntryTypeConverter : JsonConverter<LootFilterEntryType>
    {
        public override LootFilterEntryType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                int value = reader.GetInt32();
                return value == 0 ? LootFilterEntryType.ImportantLoot : LootFilterEntryType.BlacklistedLoot;
            }
            else if (reader.TokenType == JsonTokenType.String)
            {
                string? value = reader.GetString() ?? "ImportantLoot";
                return Enum.TryParse<LootFilterEntryType>(value, true, out var result) 
                    ? result 
                    : LootFilterEntryType.ImportantLoot;
            }
            return LootFilterEntryType.ImportantLoot;
        }

        public override void Write(Utf8JsonWriter writer, LootFilterEntryType value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue((int)value);
        }
    }
#nullable restore

    #endregion
}
