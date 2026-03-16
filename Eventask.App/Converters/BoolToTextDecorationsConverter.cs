using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Eventask.App.Converters
{
    public class BoolToTextDecorationsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var isTrue = value as bool? ?? false;
            return isTrue ? TextDecorations.Strikethrough : null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}