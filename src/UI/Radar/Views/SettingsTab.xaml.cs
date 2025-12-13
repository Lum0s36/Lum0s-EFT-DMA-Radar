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

using LoneEftDmaRadar.UI.Radar.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace LoneEftDmaRadar.UI.Radar.Views
{
    /// <summary>
    /// Interaction logic for SettingsTab.xaml
    /// </summary>
    public partial class SettingsTab : UserControl
    {
        public SettingsViewModel ViewModel { get; }
        public SettingsTab()
        {
            InitializeComponent();
            DataContext = ViewModel = new SettingsViewModel(this);
            
            // Subscribe to toggle button events
            BtnLootFilters.Checked += UpdateLayout;
            BtnLootFilters.Unchecked += UpdateLayout;
            BtnStaticContainers.Checked += UpdateLayout;
            BtnStaticContainers.Unchecked += UpdateLayout;
            
            // Set initial layout state
            UpdateLayout(null, null);
        }

        private void UpdateLayout(object sender, RoutedEventArgs e)
        {
            bool lootFiltersChecked = BtnLootFilters.IsChecked == true;
            bool containersChecked = BtnStaticContainers.IsChecked == true;

            if (lootFiltersChecked && containersChecked)
            {
                // Both checked: split view (50/50)
                LootFiltersBorder.SetValue(Grid.ColumnProperty, 0);
                LootFiltersBorder.SetValue(Grid.ColumnSpanProperty, 1);
                ContainersBorder.SetValue(Grid.ColumnProperty, 2);
                ContainersBorder.SetValue(Grid.ColumnSpanProperty, 1);
                SplitterColumn.Width = new GridLength(5);
            }
            else if (lootFiltersChecked)
            {
                // Only Loot Filters: full width
                LootFiltersBorder.SetValue(Grid.ColumnProperty, 0);
                LootFiltersBorder.SetValue(Grid.ColumnSpanProperty, 3);
                ContainersBorder.SetValue(Grid.ColumnProperty, 2);
                ContainersBorder.SetValue(Grid.ColumnSpanProperty, 1);
                SplitterColumn.Width = new GridLength(0);
            }
            else if (containersChecked)
            {
                // Only Static Containers: full width
                ContainersBorder.SetValue(Grid.ColumnProperty, 0);
                ContainersBorder.SetValue(Grid.ColumnSpanProperty, 3);
                LootFiltersBorder.SetValue(Grid.ColumnProperty, 0);
                LootFiltersBorder.SetValue(Grid.ColumnSpanProperty, 1);
                SplitterColumn.Width = new GridLength(0);
            }
            else
            {
                // Both unchecked: hide splitter
                SplitterColumn.Width = new GridLength(0);
            }
        }

    }
}
