using System.Windows.Controls;
using LoneEftDmaRadar.UI.Radar.ViewModels;

namespace LoneEftDmaRadar.UI.Radar.Views
{
    public partial class DebugTab : UserControl
    {
        public DebugTabViewModel ViewModel { get; }

        public DebugTab()
        {
            InitializeComponent();
            DataContext = ViewModel = new DebugTabViewModel();
        }
    }
}
