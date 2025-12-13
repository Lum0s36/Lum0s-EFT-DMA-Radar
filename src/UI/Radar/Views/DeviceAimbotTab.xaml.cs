using LoneEftDmaRadar.UI.Radar.ViewModels;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace LoneEftDmaRadar.UI.Radar.Views
{
    public partial class DeviceAimbotTab : UserControl
    {
        public DeviceAimbotViewModel ViewModel { get; }

        public DeviceAimbotTab()
        {
            InitializeComponent();
            DataContext = ViewModel = new DeviceAimbotViewModel();
        }
    } 
}