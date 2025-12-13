using LoneEftDmaRadar.UI.Radar.ViewModels;
using System.Windows.Controls;

namespace LoneEftDmaRadar.UI.Radar.Views
{
    /// <summary>
    /// Interaction logic for EspSettingsTab.xaml
    /// </summary>
    public partial class EspSettingsTab : UserControl
    {
        public EspSettingsViewModel ViewModel { get; }

        public EspSettingsTab()
        {
            InitializeComponent();
            DataContext = ViewModel = new EspSettingsViewModel();
        }
    }
}

