/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Tarkov.GameWorld.Quests;
using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.Web.TarkovDev.Data;
using System.Collections.Frozen;

namespace LoneEftDmaRadar.Tarkov
{
    /// <summary>
    /// Manages Tarkov Dynamic Data (Items, Quests, etc).
    /// Data types are defined in TarkovDataTypes.cs as a partial class.
    /// </summary>
    public static partial class TarkovDataManager
    {
        private static readonly FileInfo _bakDataFile = new(Path.Combine(App.ConfigPath.FullName, "data.json.bak"));
        private static readonly FileInfo _tempDataFile = new(Path.Combine(App.ConfigPath.FullName, "data.json.tmp"));
        private static readonly FileInfo _dataFile = new(Path.Combine(App.ConfigPath.FullName, "data.json"));
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        /// <summary>
        /// Master items dictionary - mapped via BSGID String.
        /// </summary>
        public static FrozenDictionary<string, TarkovMarketItem> AllItems { get; private set; }

        /// <summary>
        /// Master containers dictionary - mapped via BSGID String.
        /// </summary>
        public static FrozenDictionary<string, TarkovMarketItem> AllContainers { get; private set; }

        /// <summary>
        /// Maps Data for Tarkov.
        /// </summary>
        public static FrozenDictionary<string, MapElement> MapData { get; private set; }

        /// <summary>
        /// Tasks Data for Tarkov.
        /// </summary>
        public static FrozenDictionary<string, TaskElement> TaskData { get; private set; }

        /// <summary>
        /// All Task Zones mapped by MapID -> ZoneID -> Position.
        /// </summary>
        public static FrozenDictionary<string, FrozenDictionary<string, Vector3>> TaskZones { get; private set; }

        /// <summary>
        /// XP Table for Tarkov.
        /// </summary>
        public static IReadOnlyDictionary<int, int> XPTable { get; private set; }

        /// <summary>
        /// Event raised when progress is updated during startup.
        /// </summary>
        public static event Action<string> OnProgressUpdate;

        #region Startup

        /// <summary>
        /// Call to start EftDataManager Module. ONLY CALL ONCE.
        /// </summary>
        /// <param name="loading">Loading UI Form.</param>
        /// <param name="defaultOnly">True if you want to load cached/default data only.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task ModuleInitAsync(bool defaultOnly = false)
        {
            try
            {
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"ERROR loading Game/Loot Data ({_dataFile.Name})", ex);
            }
        }

        /// <summary>
        /// Refresh data from API. If API fails, keeps existing cached data.
        /// </summary>
        public static async Task RefreshFromApiAsync()
        {
            try
            {
                string dataJson = await TarkovDevDataJob.GetUpdatedDataAsync();
                if (string.IsNullOrEmpty(dataJson))
                    throw new InvalidOperationException("API returned empty data");

                await File.WriteAllTextAsync(_tempDataFile.FullName, dataJson);
                if (_dataFile.Exists)
                {
                    File.Replace(
                        sourceFileName: _tempDataFile.FullName,
                        destinationFileName: _dataFile.FullName,
                        destinationBackupFileName: _bakDataFile.FullName,
                        ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(_tempDataFile.FullName, _dataFile.FullName, overwrite: true);
                }

                var data = JsonSerializer.Deserialize<TarkovData>(dataJson, _jsonOptions);
                if (data != null)
                    SetData(data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TarkovDataManager] API refresh failed: {ex.Message}");
            }
        }

        #endregion

        #region Methods

        private static async Task LoadDataAsync()
        {
            OnProgressUpdate?.Invoke("Loading cached data...");

            if (_dataFile.Exists)
            {
                try
                {
                    await LoadDiskDataAsync();
                    DebugLogger.LogDebug($"[TarkovDataManager] Loaded cached data: Items={AllItems?.Count ?? 0}, Tasks={TaskData?.Count ?? 0}");
                }
                catch
                {
                    await LoadDefaultDataAsync();
                }
            }
            else
            {
                await LoadDefaultDataAsync();
            }

            OnProgressUpdate?.Invoke("Fetching fresh data from tarkov.dev...");
            await LoadRemoteDataAsync();
        }

        private static void SetData(TarkovData data)
        {
            if (data == null) return;

            AllItems = (data.Items ?? new List<TarkovMarketItem>())
                .Where(x => x != null && (!x.Tags?.Contains("Static Container") ?? false))
                .DistinctBy(x => x.BsgId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(k => k.BsgId, v => v, StringComparer.OrdinalIgnoreCase)
                .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

            AllContainers = (data.Items ?? new List<TarkovMarketItem>())
                .Where(x => x != null && (x.Tags?.Contains("Static Container") ?? false))
                .DistinctBy(x => x.BsgId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(k => k.BsgId, v => v, StringComparer.OrdinalIgnoreCase)
                .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

            TaskData = (data.Tasks ?? new List<TaskElement>())
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Id))
                .DistinctBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(t => t.Id, t => t, StringComparer.OrdinalIgnoreCase)
                .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

            DebugLogger.LogDebug($"[TarkovDataManager] SetData: Items={AllItems.Count}, Containers={AllContainers.Count}, Tasks={TaskData.Count}");

            try
            {
                TaskZones = TaskData.Values
                    .Where(task => task?.Objectives != null)
                    .SelectMany(task => task.Objectives)
                    .Where(objective => objective?.Zones != null)
                    .SelectMany(objective => objective.Zones)
                    .Where(zone => zone?.Position != null && zone.Map?.NameId != null)
                    .GroupBy(zone => zone.Map.NameId, zone => new
                    {
                        id = zone.Id,
                        pos = new Vector3(zone.Position.X, zone.Position.Y, zone.Position.Z)
                    }, StringComparer.OrdinalIgnoreCase)
                    .DistinctBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .Where(x => !string.IsNullOrEmpty(x.id))
                            .DistinctBy(x => x.id, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(zone => zone.id, zone => zone.pos, StringComparer.OrdinalIgnoreCase)
                            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                        StringComparer.OrdinalIgnoreCase)
                    .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                TaskZones = new Dictionary<string, FrozenDictionary<string, Vector3>>()
                    .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            }

            XPTable = data.PlayerLevels?.ToDictionary(x => x.Exp, x => x.Level) ?? new Dictionary<int, int>();

            var maps = (data.Maps ?? new List<MapElement>())
                .Where(m => m != null && !string.IsNullOrEmpty(m.NameId))
                .ToDictionary(x => x.NameId, StringComparer.OrdinalIgnoreCase);
            maps.TryAdd("Terminal", new MapElement { Name = "Terminal", NameId = "Terminal" });
            MapData = maps.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }

        private static async Task LoadDefaultDataAsync()
        {
            const string resource = "LoneEftDmaRadar.DEFAULT_DATA.json";
            using var dataStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource) ??
                throw new ArgumentNullException(resource);
            var data = await JsonSerializer.DeserializeAsync<TarkovData>(dataStream)
                ?? throw new InvalidOperationException($"Failed to deserialize {nameof(dataStream)}");
            SetData(data);
        }

        private static async Task LoadDiskDataAsync()
        {
            var data = await TryLoadFromDiskAsync(_tempDataFile) ??
                await TryLoadFromDiskAsync(_dataFile) ??
                await TryLoadFromDiskAsync(_bakDataFile);

            if (data is null)
            {
                _dataFile.Delete();
                await LoadDefaultDataAsync();
                return;
            }

            SetData(data);

            static async Task<TarkovData> TryLoadFromDiskAsync(FileInfo file)
            {
                try
                {
                    if (!file.Exists) return null;
                    using var dataStream = File.OpenRead(file.FullName);
                    return await JsonSerializer.DeserializeAsync<TarkovData>(dataStream, _jsonOptions);
                }
                catch
                {
                    return null;
                }
            }
        }

        private static async Task LoadRemoteDataAsync()
        {
            try
            {
                OnProgressUpdate?.Invoke("Connecting to tarkov.dev API...");
                string dataJson = await TarkovDevDataJob.GetUpdatedDataAsync();
                ArgumentNullException.ThrowIfNull(dataJson, nameof(dataJson));

                OnProgressUpdate?.Invoke("Saving data to cache...");
                await File.WriteAllTextAsync(_tempDataFile.FullName, dataJson);

                if (_dataFile.Exists)
                {
                    File.Replace(
                        sourceFileName: _tempDataFile.FullName,
                        destinationFileName: _dataFile.FullName,
                        destinationBackupFileName: _bakDataFile.FullName,
                        ignoreMetadataErrors: true);
                }
                else
                {
                    File.Copy(sourceFileName: _tempDataFile.FullName, destFileName: _bakDataFile.FullName, overwrite: true);
                    File.Move(sourceFileName: _tempDataFile.FullName, destFileName: _dataFile.FullName, overwrite: true);
                }

                OnProgressUpdate?.Invoke("Processing game data...");
                var data = JsonSerializer.Deserialize<TarkovData>(dataJson, _jsonOptions) ??
                    throw new InvalidOperationException($"Failed to deserialize {nameof(dataJson)}");
                SetData(data);

                OnProgressUpdate?.Invoke($"Loaded {AllItems?.Count ?? 0} items, {TaskData?.Count ?? 0} tasks");
                DebugLogger.LogDebug($"[TarkovDataManager] Successfully loaded remote data");
            }
            catch (Exception ex)
            {
                OnProgressUpdate?.Invoke("API failed, using cached data");
                DebugLogger.LogDebug($"[TarkovDataManager] LoadRemoteDataAsync failed: {ex.Message}");
            }
        }

        #endregion
    }
}