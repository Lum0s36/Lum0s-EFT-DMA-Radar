using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LoneEftDmaRadar.UI.Hotkeys
{
    /// <summary>
    /// Simple converter to highlight the currently listening hotkey row.
    /// </summary>
    public sealed class BooleanToBrushConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; } = new SolidColorBrush(Color.FromArgb(40, 0, 120, 215));
        public Brush FalseBrush { get; set; } = Brushes.Transparent;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return TrueBrush;
            return FalseBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
