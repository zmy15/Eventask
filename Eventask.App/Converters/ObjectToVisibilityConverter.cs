using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Eventask.App.Converters
{
    public class ObjectToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}