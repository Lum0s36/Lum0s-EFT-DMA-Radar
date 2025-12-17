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

using System.ComponentModel;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Quests
{
    /// <summary>
    /// One-Way Binding Only
    /// </summary>
    public sealed class QuestEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public string Id { get; }
        public string Name { get; }
        
        /// <summary>
        /// Completed objective condition IDs for this quest.
        /// </summary>
        public HashSet<string> CompletedConditions { get; } = new(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// Objective progress counters (ObjectiveId -> CurrentCount).
        /// </summary>
        public ConcurrentDictionary<string, int> ConditionCounters { get; } = new(StringComparer.OrdinalIgnoreCase);
        
        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _isEnabled = value;
                if (value) // Enabled
                {
                    App.Config.QuestHelper.BlacklistedQuests.TryRemove(Id, out _);
                }
                else
                {
                    App.Config.QuestHelper.BlacklistedQuests.TryAdd(Id, 0);
                }
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        public QuestEntry(string id)
        {
            Id = id;
            if (TarkovDataManager.TaskData.TryGetValue(id, out var task))
            {
                Name = task.Name ?? id;
            }
            else
            {
                Name = id;
            }
            _isEnabled = !App.Config.QuestHelper.BlacklistedQuests.ContainsKey(id);
        }
        
        /// <summary>
        /// Check if a specific objective is completed.
        /// </summary>
        public bool IsObjectiveCompleted(string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId))
                return false;
            return CompletedConditions.Contains(objectiveId);
        }
        
        /// <summary>
        /// Get the current progress count for an objective.
        /// </summary>
        public int GetObjectiveProgress(string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId))
                return 0;
            return ConditionCounters.TryGetValue(objectiveId, out var count) ? count : 0;
        }
        
        /// <summary>
        /// Update completed conditions from memory.
        /// </summary>
        internal void UpdateCompletedConditions(IEnumerable<string> completedIds)
        {
            CompletedConditions.Clear();
            foreach (var id in completedIds)
            {
                if (!string.IsNullOrEmpty(id))
                    CompletedConditions.Add(id);
            }
        }
        
        /// <summary>
        /// Update condition counters from memory.
        /// </summary>
        internal void UpdateConditionCounters(IEnumerable<KeyValuePair<string, int>> counters)
        {
            ConditionCounters.Clear();
            foreach (var kvp in counters)
            {
                if (!string.IsNullOrEmpty(kvp.Key))
                    ConditionCounters[kvp.Key] = kvp.Value;
            }
        }

        public override string ToString() => Name;
    }
}
