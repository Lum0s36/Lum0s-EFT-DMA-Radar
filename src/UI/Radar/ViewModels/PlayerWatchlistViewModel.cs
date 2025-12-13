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

using LoneEftDmaRadar.UI.Data;
using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.UI.Radar.Views;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace LoneEftDmaRadar.UI.Radar.ViewModels
{
    public sealed class PlayerWatchlistViewModel : INotifyPropertyChanged
    {
        private readonly PlayerWatchlistTab _parent;
        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        private readonly ConcurrentDictionary<string, PlayerWatchlistEntry> _watchlist = new(App.Config.PlayerWatchlist
            .GroupBy(p => p.AcctID, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                k => k.Key, v => v.First(),
                StringComparer.OrdinalIgnoreCase));
        /// <summary>
        /// Thread Safe Watchlist for Lookups.
        /// </summary>
        public IReadOnlyDictionary<string, PlayerWatchlistEntry> Watchlist => _watchlist;
        /// <summary>
        /// Entries for the Player Watchlist (Data Binding Only).
        /// </summary>
        public ObservableCollection<PlayerWatchlistEntry> Entries => App.Config.PlayerWatchlist;

        public ObservableCollection<string> RaidPlayers { get; } = new();
        private string _selectedRaidPlayerId;
        public string SelectedRaidPlayerId
        {
            get => _selectedRaidPlayerId;
            set { if (_selectedRaidPlayerId != value) { _selectedRaidPlayerId = value; OnPropertyChanged(); } }
        }
        private string _reasonInput;
        public string ReasonInput
        {
            get => _reasonInput;
            set { if (_reasonInput != value) { _reasonInput = value; OnPropertyChanged(); } }
        }
        private bool _isStreamerInput;
        public bool IsStreamerInput
        {
            get => _isStreamerInput;
            set { if (_isStreamerInput != value) { _isStreamerInput = value; OnPropertyChanged(); } }
        }
        public ICommand RefreshRaidPlayersCommand { get; }
        public ICommand AddSelectedRaidPlayerCommand { get; }

        private void RefreshRaidPlayers()
        {
            RaidPlayers.Clear();
            var players = Memory.Players;
            if (players != null)
            {
                foreach (var p in players.Where(p => p.IsHuman))
                {
                    var id = p.AccountID ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(id)) RaidPlayers.Add(id);
                }
            }
        }

        private void AddSelectedRaidPlayer()
        {
            var acct = SelectedRaidPlayerId;
            if (string.IsNullOrWhiteSpace(acct)) return;
            var reason = string.IsNullOrWhiteSpace(ReasonInput) ? "Cheater" : ReasonInput;
            var entry = new PlayerWatchlistEntry { AcctID = acct, Reason = reason, Streamer = IsStreamerInput, Timestamp = DateTime.Now };
            var existing = Entries.FirstOrDefault(x => string.Equals(x.AcctID, acct, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.Reason = $"{reason} | {existing.Reason}";
                existing.Streamer = existing.Streamer || IsStreamerInput;
            }
            else
            {
                Entries.Add(entry);
            }
        }

        private void Entries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add &&
                e.NewItems is not null)
            {
                foreach (PlayerWatchlistEntry entry in e.NewItems)
                {
                    _watchlist.TryAdd(entry.AcctID, entry);
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove &&
                e.OldItems is not null)
            {
                foreach (PlayerWatchlistEntry entry in e.OldItems)
                {
                    _watchlist.TryRemove(entry.AcctID, out _);
                }
            }
        }

        private PlayerWatchlistEntry _selectedEntry;
        public PlayerWatchlistEntry SelectedEntry
        {
            get => _selectedEntry;
            set
            {
                if (_selectedEntry != value)
                {
                    _selectedEntry = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Static helper.
        /// </summary>
        /// <param name="entry"></param>
        public static void Add(PlayerWatchlistEntry entry)
        {
            if (MainWindow.Instance?.PlayerWatchlist is PlayerWatchlistTab playerWatchlist)
            {
                playerWatchlist.Dispatcher.Invoke(() =>
                {
                    // Add the entry to the watchlist
                    if (playerWatchlist.ViewModel?.Entries is not null)
                    {
                        var existing = playerWatchlist.ViewModel.Entries.FirstOrDefault(x => string.Equals(x.AcctID, entry.AcctID, StringComparison.OrdinalIgnoreCase));
                        if (existing is not null)
                        {
                            existing.Reason = $"{entry.Reason} | {existing.Reason}";
                        }
                        else
                        {
                            playerWatchlist.ViewModel.Entries.Add(entry);
                        }
                    }
                });
            }
        }

        public PlayerWatchlistViewModel(PlayerWatchlistTab parent)
        {
            _parent = parent;
            Entries.CollectionChanged += Entries_CollectionChanged;
            RefreshRaidPlayersCommand = new SimpleCommand(RefreshRaidPlayers);
            AddSelectedRaidPlayerCommand = new SimpleCommand(AddSelectedRaidPlayer);

            // Normalize existing entries: set Streamer=true if Reason mentions Twitch/Youtube/YT
            foreach (var e in Entries.ToList())
            {
                var r = e.Reason?.ToLowerInvariant() ?? string.Empty;
                if (!e.Streamer && (r.Contains("twitch") || r.Contains("youtube") || r.Contains("yt")))
                {
                    e.Streamer = true;
                }
            }
        }
    }
}
