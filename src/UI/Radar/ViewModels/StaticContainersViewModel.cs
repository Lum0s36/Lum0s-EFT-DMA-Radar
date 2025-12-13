using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using LoneEftDmaRadar.Tarkov;
using LoneEftDmaRadar.UI.Data;

namespace LoneEftDmaRadar.UI.Radar.ViewModels
{
    public sealed class StaticContainersViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly ObservableCollection<StaticContainerEntry> _allContainers = new();
        private ICollectionView _filteredContainers;
        private string _searchText = string.Empty;

        public StaticContainersViewModel()
        {
            InitializeContainers();
            _filteredContainers = CollectionViewSource.GetDefaultView(_allContainers);
            _filteredContainers.Filter = FilterContainer;
        }

        public ICollectionView StaticContainers => _filteredContainers;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value ?? string.Empty;
                OnPropertyChanged(nameof(SearchText));
                _filteredContainers?.Refresh();
            }
        }

        private bool FilterContainer(object obj)
        {
            if (obj is not StaticContainerEntry entry)
                return false;

            if (string.IsNullOrWhiteSpace(_searchText))
                return true;

            var searchLower = _searchText.ToLowerInvariant();
            return entry.Name?.ToLowerInvariant().Contains(searchLower) == true ||
                   entry.Id?.ToLowerInvariant().Contains(searchLower) == true;
        }

        public bool StaticContainersSelectAll
        {
            get => App.Config.Containers.SelectAll;
            set
            {
                if (App.Config.Containers.SelectAll != value)
                {
                    App.Config.Containers.SelectAll = value;
                    foreach (var item in _allContainers)
                        item.IsTracked = value;
                    OnPropertyChanged(nameof(StaticContainersSelectAll));
                }
            }
        }

        public bool HideSearchedContainers
        {
            get => App.Config.Containers.HideSearched;
            set
            {
                if (App.Config.Containers.HideSearched != value)
                {
                    App.Config.Containers.HideSearched = value;
                    OnPropertyChanged(nameof(HideSearchedContainers));
                }
            }
        }

        private void InitializeContainers()
        {
            var entries = TarkovDataManager.AllContainers.Values
                .OrderBy(x => x.Name)
                .Select(x => new StaticContainerEntry(x));
            foreach (var entry in entries)
            {
                entry.PropertyChanged += Entry_PropertyChanged;
                _allContainers.Add(entry);
            }

            // If SelectAll is true but Selected dictionary is empty, populate it
            // Ensures containers remain visible after unchecking some
            if (App.Config.Containers.SelectAll && App.Config.Containers.Selected.IsEmpty)
            {
                foreach (var entry in _allContainers)
                {
                    App.Config.Containers.Selected.TryAdd(entry.Id, 0);
                }
            }
        }

        private void Entry_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StaticContainerEntry.IsTracked))
            {
                var allSelected = _allContainers.All(x => x.IsTracked);
                if (App.Config.Containers.SelectAll != allSelected)
                {
                    App.Config.Containers.SelectAll = allSelected;
                    OnPropertyChanged(nameof(StaticContainersSelectAll));
                }
            }
        }

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
