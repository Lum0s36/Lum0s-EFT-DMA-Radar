/*
 * Quest Helper Tab - Code Behind
 * Simple and clean implementation
 */

using System.Windows;
using System.Windows.Controls;
using LoneEftDmaRadar.UI.Radar.ViewModels;

namespace LoneEftDmaRadar.UI.Radar.Views
{
    public partial class QuestHelperTab : UserControl
    {
        public QuestHelperViewModel ViewModel { get; }

        public QuestHelperTab()
        {
            InitializeComponent();
            ViewModel = new QuestHelperViewModel();
            DataContext = ViewModel;
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleSelectAll();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // Disable button during refresh
            if (sender is Button button)
            {
                button.IsEnabled = false;
                button.Content = "Loading...";
            }

            try
            {
                // Refresh from API (with fallback to cache)
                await ViewModel.RefreshFromApiAsync();
            }
            finally
            {
                if (sender is Button btn)
                {
                    btn.IsEnabled = true;
                    btn.Content = "Refresh";
                }
            }
        }

        private void ExpandButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is QuestTrackingEntry quest)
            {
                quest.ToggleExpanded();
            }
        }
    }
}
