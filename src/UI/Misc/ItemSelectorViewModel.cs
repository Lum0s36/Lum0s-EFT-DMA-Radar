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

using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using LoneEftDmaRadar.Web.TarkovDev.Data;
using System.Collections.Generic;

namespace LoneEftDmaRadar.UI.Misc
{
    public sealed class ItemSelectorViewModel : INotifyPropertyChanged
    {
        private readonly ObservableCollection<TarkovMarketItem> _allItems;
        private ICollectionView _filteredItems;
        private string _searchText = string.Empty;
        private TarkovMarketItem _selectedItem;

        public ItemSelectorViewModel(ObservableCollection<TarkovMarketItem> items)
        {
            _allItems = items;
            // Use ListCollectionView to support CustomSort
            var listCollection = new ListCollectionView(items.ToList());
            listCollection.Filter = FilterItem;
            _filteredItems = listCollection;
            
            OkCommand = new SimpleCommand(() => { if (HasSelection) OnCloseRequested(true); });
            CancelCommand = new SimpleCommand(() => OnCloseRequested(false));
        }

        public ICollectionView FilteredItems => _filteredItems;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value ?? string.Empty;
                OnPropertyChanged(nameof(SearchText));
                _filteredItems?.Refresh();
                ApplySorting();
            }
        }

        private void ApplySorting()
        {
            if (_filteredItems is ListCollectionView listView)
            {
                listView.CustomSort = string.IsNullOrWhiteSpace(_searchText) 
                    ? null 
                    : new ItemSearchComparer(() => _searchText);
            }
            else if (_filteredItems != null)
            {
                // If not ListCollectionView, recreate it
                var items = _allItems.ToList();
                var listCollection = new ListCollectionView(items);
                listCollection.Filter = FilterItem;
                listCollection.CustomSort = string.IsNullOrWhiteSpace(_searchText) 
                    ? null 
                    : new ItemSearchComparer(() => _searchText);
                _filteredItems = listCollection;
                OnPropertyChanged(nameof(FilteredItems));
            }
        }

        public TarkovMarketItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem == value) return;
                _selectedItem = value;
                OnPropertyChanged(nameof(SelectedItem));
                OnPropertyChanged(nameof(HasSelection));
            }
        }

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        public bool HasSelection => SelectedItem != null;

        public event EventHandler<CloseRequestedEventArgs> CloseRequested;

        private bool FilterItem(object obj)
        {
            if (obj is not TarkovMarketItem item)
                return false;

            if (string.IsNullOrWhiteSpace(_searchText))
                return true;

            var searchLower = _searchText.ToLowerInvariant();
            return item.Name?.ToLowerInvariant().Contains(searchLower) == true ||
                   item.BsgId?.ToLowerInvariant().Contains(searchLower) == true ||
                   item.ShortName?.ToLowerInvariant().Contains(searchLower) == true;
        }

        private void OnCloseRequested(bool result)
            => CloseRequested?.Invoke(this, new CloseRequestedEventArgs(result));

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        public class CloseRequestedEventArgs : EventArgs
        {
            public bool DialogResult { get; }
            public CloseRequestedEventArgs(bool dialogResult) => DialogResult = dialogResult;
        }

        private class ItemSearchComparer : IComparer
        {
            private readonly Func<string> _getSearchText;

            public ItemSearchComparer(Func<string> getSearchText)
            {
                _getSearchText = getSearchText;
            }

            public int Compare(object x, object y)
            {
                if (x is not TarkovMarketItem itemX || y is not TarkovMarketItem itemY)
                    return 0;

                var searchText = _getSearchText();
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    // No search text, sort alphabetically by name
                    return string.Compare(itemX.Name, itemY.Name, StringComparison.OrdinalIgnoreCase);
                }

                var searchLower = searchText.ToLowerInvariant();
                var nameX = itemX.Name?.ToLowerInvariant() ?? string.Empty;
                var nameY = itemY.Name?.ToLowerInvariant() ?? string.Empty;
                var shortNameX = itemX.ShortName?.ToLowerInvariant() ?? string.Empty;
                var shortNameY = itemY.ShortName?.ToLowerInvariant() ?? string.Empty;

                // Priority 1: Starts with search text (name)
                bool xStartsWith = nameX.StartsWith(searchLower);
                bool yStartsWith = nameY.StartsWith(searchLower);

                if (xStartsWith && !yStartsWith) return -1;
                if (!xStartsWith && yStartsWith) return 1;

                // Priority 2: Starts with search text (short name)
                bool xShortStartsWith = shortNameX.StartsWith(searchLower);
                bool yShortStartsWith = shortNameY.StartsWith(searchLower);

                if (xShortStartsWith && !yShortStartsWith) return -1;
                if (!xShortStartsWith && yShortStartsWith) return 1;

                // Priority 3: Contains search text (name)
                bool xContains = nameX.Contains(searchLower);
                bool yContains = nameY.Contains(searchLower);

                if (xContains && !yContains) return -1;
                if (!xContains && yContains) return 1;

                // Priority 4: Contains search text (short name)
                bool xShortContains = shortNameX.Contains(searchLower);
                bool yShortContains = shortNameY.Contains(searchLower);

                if (xShortContains && !yShortContains) return -1;
                if (!xShortContains && yShortContains) return 1;

                // Priority 5: Alphabetical order
                return string.Compare(itemX.Name, itemY.Name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}

