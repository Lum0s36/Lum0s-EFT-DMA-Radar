using System.Windows.Controls;
using LoneEftDmaRadar.UI.Radar.ViewModels;

namespace LoneEftDmaRadar.UI.Radar.Views
{
    /// <summary>
    /// Static container settings tab (shared by radar and ESP).
    /// </summary>
    public partial class StaticContainersTab : UserControl
    {
        public StaticContainersViewModel ViewModel { get; }

        public StaticContainersTab()
        {
            InitializeComponent();
            DataContext = ViewModel = new StaticContainersViewModel();
        }
    }
}
