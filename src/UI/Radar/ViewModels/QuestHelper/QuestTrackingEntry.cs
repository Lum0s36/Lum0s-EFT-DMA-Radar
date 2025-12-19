/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using LoneEftDmaRadar.Tarkov;
using LoneEftDmaRadar.Tarkov.GameWorld.Quests;
using LoneEftDmaRadar.UI.Misc;

namespace LoneEftDmaRadar.UI.Radar.ViewModels.QuestHelper
{
    /// <summary>
    /// Quest entry for the tracking list with expandable objectives.
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
                DebugLogger.LogDebug($"[QuestTrackingEntry] QuestManager is null");
                return;
            }

            if (!questManager.Quests.TryGetValue(QuestId, out var questEntry))
            {
                DebugLogger.LogDebug($"[QuestTrackingEntry] Quest {QuestId} not found in active quests");
                return;
            }

            int completedCount = 0;
            int updatedCount = 0;

            foreach (var obj in Objectives)
            {
                if (string.IsNullOrEmpty(obj.ObjectiveId))
                    continue;

                var wasCompleted = obj.IsCompleted;
                var isCompleted = questEntry.IsObjectiveCompleted(obj.ObjectiveId);

                var oldProgress = obj.CurrentCount;
                var progress = questEntry.GetObjectiveProgress(obj.ObjectiveId);
                obj.CurrentCount = progress;

                var targetFromMemory = questEntry.GetObjectiveTargetCount(obj.ObjectiveId);
                if (targetFromMemory > 0 && targetFromMemory != obj.Count)
                    obj.Count = targetFromMemory;

                if (!isCompleted && obj.Count > 0 && progress >= obj.Count)
                    isCompleted = true;

                obj.IsCompleted = isCompleted;

                if (isCompleted)
                    completedCount++;

                if (progress != oldProgress || wasCompleted != isCompleted)
                    updatedCount++;
            }

            if (updatedCount > 0 || completedCount > 0)
            {
                DebugLogger.LogDebug($"[QuestTrackingEntry] Quest {QuestName}: {completedCount} completed, {updatedCount} updated");
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

            var questManager = Memory.Game?.QuestManager;
            QuestEntry questEntry = null;
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
                    ProcessObjective(obj, questEntry, keyNames, questItemNames, regularItemNames);
                }
            }

            BuildHeaderInfo(keyNames, questItemNames, regularItemNames);
            OnPropertyChanged(nameof(HasNoObjectives));
            OnPropertyChanged(nameof(ObjectivesHeaderText));
        }

        private void ProcessObjective(
            TarkovDataManager.TaskElement.ObjectiveElement obj,
            QuestEntry questEntry,
            List<string> keyNames,
            List<string> questItemNames,
            List<string> regularItemNames)
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

            bool isCompleted = false;
            int currentCount = 0;
            int targetCount = count;

            if (questEntry != null && !string.IsNullOrEmpty(obj.Id))
            {
                isCompleted = questEntry.IsObjectiveCompleted(obj.Id);
                currentCount = questEntry.GetObjectiveProgress(obj.Id);

                var memoryTargetCount = questEntry.GetObjectiveTargetCount(obj.Id);
                if (memoryTargetCount > 0)
                    targetCount = memoryTargetCount;

                if (!isCompleted && targetCount > 0 && currentCount >= targetCount)
                    isCompleted = true;
            }

            Objectives.Add(new QuestObjectiveEntry
            {
                ObjectiveId = obj.Id,
                Type = obj.Type,
                Description = obj.Description ?? QuestObjectiveHelper.GetDefaultDescription(obj),
                Count = targetCount,
                TypeIcon = QuestObjectiveHelper.GetIcon(obj.Type),
                ZoneCount = obj.Zones?.Count ?? 0,
                ItemName = obj.Item?.Name ?? obj.QuestItem?.Name,
                MapName = obj.Maps?.FirstOrDefault()?.Name,
                IsCompleted = isCompleted,
                CurrentCount = currentCount
            });
        }

        private void BuildHeaderInfo(List<string> keyNames, List<string> questItemNames, List<string> regularItemNames)
        {
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
        }
    }
}
