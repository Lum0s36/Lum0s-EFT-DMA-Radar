/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using Microsoft.Win32;
using SkiaSharp;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace LoneEftDmaRadar.UI.Loot.Helpers
{
    /// <summary>
    /// Handles import and export of loot filters.
    /// </summary>
    public static class LootFilterImportExportHelper
    {
        private static readonly JsonSerializerOptions ExportOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static readonly JsonSerializerOptions ExportCompactOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static readonly JsonSerializerOptions ImportOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        #region Export

        /// <summary>
        /// Exports all filters. Returns true if export was successful.
        /// </summary>
        public static bool ExportFilters(Window ownerWindow)
        {
            try
            {
                var exportMethod = MessageBox.Show(
                    ownerWindow,
                    "Choose export method:\n\n" +
                    "Yes = Export to File (JSON)\n" +
                    "No = Export as Compact Text (Base64)\n" +
                    "Cancel = Abort",
                    "Export Loot Filters",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (exportMethod == MessageBoxResult.Cancel)
                    return false;

                var exportData = CreateExportData();

                if (exportMethod == MessageBoxResult.Yes)
                {
                    return ExportToFile(ownerWindow, exportData);
                }
                else
                {
                    return ExportToBase64(ownerWindow, exportData);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ownerWindow,
                    $"ERROR Exporting Filters: {ex.Message}",
                    "Export Loot Filters",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        private static LootFiltersExportData CreateExportData()
        {
            return new LootFiltersExportData
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
        }

        private static bool ExportToFile(Window ownerWindow, LootFiltersExportData exportData)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = "json",
                FileName = "loot_filters_export.json",
                Title = "Export Loot Filters to File"
            };

            if (dialog.ShowDialog() != true)
                return false;

            var json = JsonSerializer.Serialize(exportData, ExportOptions);
            File.WriteAllText(dialog.FileName, json);

            int filterCount = exportData.Filters?.Count ?? 0;
            MessageBox.Show(
                ownerWindow,
                $"Successfully exported {filterCount} filter(s) to:\n{dialog.FileName}",
                "Export Loot Filters",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return true;
        }

        private static bool ExportToBase64(Window ownerWindow, LootFiltersExportData exportData)
        {
            var json = JsonSerializer.Serialize(exportData, ExportCompactOptions);
            var jsonBytes = Encoding.UTF8.GetBytes(json);

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
            int filterCount = exportData.Filters?.Count ?? 0;

            ShowBase64ExportWindow(ownerWindow, base64, filterCount, jsonBytes.Length, compressedBytes.Length);
            return true;
        }

        private static void ShowBase64ExportWindow(Window ownerWindow, string base64, int filterCount, int originalSize, int compressedSize)
        {
            var resultWindow = new Window
            {
                Title = "Export Loot Filters - Compact Text",
                Width = 700,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = ownerWindow,
                ResizeMode = ResizeMode.CanResize
            };

            var grid = new System.Windows.Controls.Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            var compressionRatio = originalSize > 0 ? (1.0 - (double)compressedSize / originalSize) * 100 : 0;

            var infoText = new System.Windows.Controls.TextBlock
            {
                Text = $"Exported {filterCount} filter(s) as compressed Base64 text.\n" +
                       $"Select the text below and copy it manually (Ctrl+C).\n\n" +
                       $"Length: {base64.Length} characters\n" +
                       $"Compression: {compressionRatio:F1}% smaller",
                TextWrapping = TextWrapping.Wrap
            };
            System.Windows.Controls.Grid.SetRow(infoText, 0);

            var textBox = new System.Windows.Controls.TextBox
            {
                Text = base64,
                IsReadOnly = false,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11,
                Margin = new Thickness(0, 10, 0, 0)
            };
            System.Windows.Controls.Grid.SetRow(textBox, 1);

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

        #endregion

        #region Import

        /// <summary>
        /// Imports filters. Returns the result of the import operation.
        /// </summary>
        public static ImportResult ImportFilters(Window ownerWindow, string selectedFilterName, Action<string> onFilterAdded)
        {
            try
            {
                var importMethod = MessageBox.Show(
                    ownerWindow,
                    "Choose import method:\n\n" +
                    "Yes = Import from File (JSON)\n" +
                    "No = Import from Compact Text (Base64)\n" +
                    "Cancel = Abort",
                    "Import Loot Filters",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (importMethod == MessageBoxResult.Cancel)
                    return ImportResult.Cancelled();

                string json;
                if (importMethod == MessageBoxResult.Yes)
                {
                    json = ImportFromFile();
                    if (json == null) return ImportResult.Cancelled();
                }
                else
                {
                    json = ImportFromBase64(ownerWindow);
                    if (json == null) return ImportResult.Cancelled();
                }

                return ProcessImport(ownerWindow, json, selectedFilterName, onFilterAdded);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ownerWindow,
                    $"ERROR Importing Filters: {ex.Message}",
                    "Import Loot Filters",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return ImportResult.Error(ex.Message);
            }
        }

        private static string ImportFromFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = "json",
                Title = "Import Loot Filters from File"
            };

            if (dialog.ShowDialog() != true)
                return null;

            return File.ReadAllText(dialog.FileName);
        }

        private static string ImportFromBase64(Window ownerWindow)
        {
            var inputWindow = new Window
            {
                Title = "Import Loot Filters - Compact Text",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = ownerWindow,
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

            var stackPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(10) };
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
            okButton.Click += (s, e) => { importConfirmed = true; inputWindow.Close(); };
            cancelButton.Click += (s, e) => inputWindow.Close();

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(infoText);
            stackPanel.Children.Add(textBox);
            stackPanel.Children.Add(buttonPanel);

            inputWindow.Content = stackPanel;
            inputWindow.ShowDialog();

            if (!importConfirmed || string.IsNullOrWhiteSpace(textBox.Text))
                return null;

            return DecodeBase64Import(textBox.Text.Trim());
        }

        private static string DecodeBase64Import(string base64Text)
        {
            var compressedBytes = Convert.FromBase64String(base64Text);

            try
            {
                using var memoryStream = new MemoryStream(compressedBytes);
                using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
                using var reader = new StreamReader(gzipStream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch
            {
                // Fallback: try as plain Base64 (backward compatibility)
                return Encoding.UTF8.GetString(compressedBytes);
            }
        }

        private static ImportResult ProcessImport(Window ownerWindow, string json, string selectedFilterName, Action<string> onFilterAdded)
        {
            var importData = JsonSerializer.Deserialize<LootFiltersExportData>(json, ImportOptions);
            if (importData == null)
            {
                MessageBox.Show(ownerWindow, "Invalid or empty import file.", "Import Loot Filters",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return ImportResult.Error("Invalid or empty import file.");
            }

            var filtersToImport = ExtractFiltersToImport(importData);
            if (!filtersToImport.Any())
            {
                MessageBox.Show(ownerWindow, "No filters found in import file.", "Import Loot Filters",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return ImportResult.Error("No filters found.");
            }

            var result = MessageBox.Show(
                ownerWindow,
                $"Found {filtersToImport.Count} filter(s) to import.\n\n" +
                "How would you like to import?\n\n" +
                "Yes = Create new filters (if name exists, will add suffix)\n" +
                "No = Replace the currently selected filter\n" +
                "Cancel = Abort import",
                "Import Loot Filters",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return ImportResult.Cancelled();

            if (result == MessageBoxResult.No)
            {
                return ReplaceSelectedFilter(ownerWindow, filtersToImport, selectedFilterName);
            }
            else
            {
                return CreateNewFilters(ownerWindow, filtersToImport, onFilterAdded);
            }
        }

        private static List<FilterExportItem> ExtractFiltersToImport(LootFiltersExportData importData)
        {
            var filtersToImport = new List<FilterExportItem>();

            if (importData.LootFilters?.Filters != null)
            {
                foreach (var kvp in importData.LootFilters.Filters)
                {
                    filtersToImport.Add(new FilterExportItem
                    {
                        Name = kvp.Key,
                        Enabled = kvp.Value.Enabled,
                        Entries = kvp.Value.Entries ?? new List<EntryExportItem>()
                    });
                }
            }
            else if (importData.Filters?.Any() == true)
            {
                filtersToImport = importData.Filters;
            }

            return filtersToImport;
        }

        private static ImportResult ReplaceSelectedFilter(Window ownerWindow, List<FilterExportItem> filtersToImport, string selectedFilterName)
        {
            if (string.IsNullOrEmpty(selectedFilterName))
            {
                MessageBox.Show(ownerWindow, "No filter selected. Please select a filter first.",
                    "Import Loot Filters", MessageBoxButton.OK, MessageBoxImage.Warning);
                return ImportResult.Error("No filter selected.");
            }

            var selectedFilter = App.Config.LootFilters.Filters[selectedFilterName];
            selectedFilter.Entries.Clear();

            int totalEntries = 0;
            foreach (var filterData in filtersToImport)
            {
                if (filterData.Entries == null) continue;

                foreach (var entryData in filterData.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entryData.ItemID)) continue;

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

            if (filtersToImport.Any())
            {
                var firstFilter = filtersToImport.First();
                selectedFilter.Enabled = firstFilter.Enabled;
                selectedFilter.Color = firstFilter.Color ?? selectedFilter.Color;
            }

            MessageBox.Show(ownerWindow,
                $"Successfully imported {totalEntries} entry/entries into '{selectedFilterName}' filter.",
                "Import Loot Filters", MessageBoxButton.OK, MessageBoxImage.Information);

            return ImportResult.Success(selectedFilterName, totalEntries, ImportMode.Replace);
        }

        private static ImportResult CreateNewFilters(Window ownerWindow, List<FilterExportItem> filtersToImport, Action<string> onFilterAdded)
        {
            int imported = 0;
            int renamed = 0;
            string firstImportedName = null;

            foreach (var filterData in filtersToImport)
            {
                if (string.IsNullOrWhiteSpace(filterData.Name)) continue;

                string filterName = filterData.Name;
                int suffix = 1;
                while (App.Config.LootFilters.Filters.ContainsKey(filterName))
                {
                    filterName = $"{filterData.Name} ({suffix})";
                    suffix++;
                    renamed++;
                }

                var newFilter = new UserLootFilter
                {
                    Enabled = filterData.Enabled,
                    Color = filterData.Color ?? SKColors.Turquoise.ToString()
                };

                if (filterData.Entries != null)
                {
                    foreach (var entryData in filterData.Entries)
                    {
                        if (string.IsNullOrWhiteSpace(entryData.ItemID)) continue;

                        newFilter.Entries.Add(new LootFilterEntry
                        {
                            ItemID = entryData.ItemID,
                            Enabled = entryData.Enabled,
                            Type = entryData.Type,
                            Comment = entryData.Comment ?? string.Empty,
                            ExplicitColor = entryData.Color,
                            ParentFilter = newFilter
                        });
                    }
                }

                App.Config.LootFilters.Filters.TryAdd(filterName, newFilter);
                onFilterAdded?.Invoke(filterName);
                imported++;

                firstImportedName ??= filterName;
            }

            string message = $"Import completed:\n• Created: {imported} new filter(s)";
            if (renamed > 0)
                message += $"\n• Renamed: {renamed} filter(s) (name conflict)";

            MessageBox.Show(ownerWindow, message, "Import Loot Filters",
                MessageBoxButton.OK, MessageBoxImage.Information);

            return ImportResult.Success(firstImportedName, imported, ImportMode.CreateNew);
        }

        #endregion
    }

    #region Import Result

    public enum ImportMode
    {
        Replace,
        CreateNew
    }

    public class ImportResult
    {
        public bool IsSuccess { get; private set; }
        public bool IsCancelled { get; private set; }
        public string ErrorMessage { get; private set; }
        public string FirstFilterName { get; private set; }
        public int ImportedCount { get; private set; }
        public ImportMode Mode { get; private set; }

        public static ImportResult Success(string firstFilterName, int count, ImportMode mode) =>
            new() { IsSuccess = true, FirstFilterName = firstFilterName, ImportedCount = count, Mode = mode };

        public static ImportResult Cancelled() =>
            new() { IsCancelled = true };

        public static ImportResult Error(string message) =>
            new() { ErrorMessage = message };
    }

    #endregion

    #region Export/Import Data Structures

#nullable enable
    /// <summary>
    /// Stable export format for Loot Filters.
    /// </summary>
    internal sealed class LootFiltersExportData
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("exportDate")]
        public string? ExportDate { get; set; }

        [JsonPropertyName("filters")]
        public List<FilterExportItem>? Filters { get; set; }

        [JsonPropertyName("lootFilters")]
        public LootFiltersWrapper? LootFilters { get; set; }
    }

    internal sealed class LootFiltersWrapper
    {
        [JsonPropertyName("selected")]
        public string? Selected { get; set; }

        [JsonPropertyName("filters")]
        public Dictionary<string, FilterExportItemObject>? Filters { get; set; }
    }

    internal sealed class FilterExportItemObject
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("entries")]
        public List<EntryExportItem> Entries { get; set; } = new();
    }

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

    internal sealed class EntryExportItem
    {
        [JsonPropertyName("itemID")]
        public string ItemID { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("type")]
        [JsonConverter(typeof(EntryTypeConverter))]
        public LootFilterEntryType Type { get; set; } = LootFilterEntryType.ImportantLoot;

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonPropertyName("color")]
        public string? Color { get; set; }
    }

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
