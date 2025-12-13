using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LoneEftDmaRadar.UI.Misc
{
    /// <summary>
    /// Computes GridLength for a two-column layout based on two Expander IsExpanded states.
    /// - If both expanded => * (split) for both columns
    /// - If only left expanded => left = *, right = Auto (header only)
    /// - If only right expanded => right = *, left = Auto (header only)
    /// - If both collapsed => both Auto (headers side-by-side)
    /// </summary>
    public sealed class TwoPaneWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool leftExpanded = GetBool(values, 0);
            bool rightExpanded = GetBool(values, 1);
            string side = (parameter as string)?.ToLowerInvariant() ?? "left";

            if (leftExpanded && rightExpanded)
                return new GridLength(1, GridUnitType.Star);

            if (leftExpanded && !rightExpanded)
                return side == "left" ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;

            if (!leftExpanded && rightExpanded)
                return side == "right" ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;

            // both collapsed
            return GridLength.Auto;
        }

        private static bool GetBool(object[] arr, int idx)
        {
            if (arr == null || idx >= arr.Length) return false;
            var v = arr[idx];
            if (v is bool) return (bool)v;
            if (v is bool?) return ((bool?)v).GetValueOrDefault(false);
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
