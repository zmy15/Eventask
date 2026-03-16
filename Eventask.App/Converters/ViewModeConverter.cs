using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Eventask.App.Converters
{
    // 用于判断当前视图模式是否匹配，匹配则返回 true (显示)，否则 false
    public class ViewModeMatchConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Enum mode && parameter is string targetModeStr)
            {
                return mode.ToString() == targetModeStr;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}