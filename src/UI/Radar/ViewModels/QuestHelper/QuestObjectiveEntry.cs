/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using LoneEftDmaRadar.Tarkov;
using LoneEftDmaRadar.Tarkov.GameWorld.Quests;

namespace LoneEftDmaRadar.UI.Radar.ViewModels.QuestHelper
{
    /// <summary>
    /// Quest objective entry for UI display.
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

                if (Count > 0 && value >= Count && !_isCompleted)
                    IsCompleted = true;
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

                if (Count > 0)
                    return $"{CurrentCount}/{Count}";

                if (Type == QuestObjectiveType.Shoot && CurrentCount > 0)
                    return $"{CurrentCount}/?";

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

    /// <summary>
    /// Helper methods for quest objectives.
    /// </summary>
    public static class QuestObjectiveHelper
    {
        public static string GetDefaultDescription(TarkovDataManager.TaskElement.ObjectiveElement obj)
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

        public static string GetIcon(QuestObjectiveType type) => type switch
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
}
