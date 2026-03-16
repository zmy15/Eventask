using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Eventask.App.Converters
{
    public class BoolToOpacityConverter : IValueConverter
    {
        public double OpacityTrue { get; set; } = 1.0;
        public double OpacityFalse { get; set; } = 0.3;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var isTrue = value as bool? ?? false;
            return isTrue ? OpacityTrue : OpacityFalse;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}