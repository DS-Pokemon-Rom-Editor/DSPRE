using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace DSPRE.Avalonia.ViewModels.Controls
{
    /// <summary>
    /// Converts a bool to a DataGridLength: true → "*" (visible), false → "0" (hidden).
    /// Used to show/hide game-family-specific DataGrid columns without code-behind.
    /// </summary>
    public class BoolToGridLengthConverter : IValueConverter
    {
        public static readonly BoolToGridLengthConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? new DataGridLength(1, DataGridLengthUnitType.Star)
                             : new DataGridLength(0);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
