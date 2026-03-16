using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Eventask.App.Converters;

/// <summary>
/// 将布尔值转换为颜色的转换器
/// 支持两种使用方式：
/// 1. 通过 ConverterParameter 传递两个颜色，格式为 "TrueColor|FalseColor"
/// 2. 不传参数时使用默认的任务/日程颜色（用于搜索结果）
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    /// <summary>
    /// 静态实例，用于搜索结果等场景的默认颜色
    /// </summary>
    public static readonly BoolToColorConverter Instance = new();

    // 默认颜色：任务 (橙色) / 日程 (蓝色)
    private static readonly IBrush DefaultTrueBrush = new SolidColorBrush(Color.Parse("#FF9500"));
    private static readonly IBrush DefaultFalseBrush = new SolidColorBrush(Color.Parse("#007AFF"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue)
        {
            return DefaultFalseBrush;
        }

        // 如果有参数，解析参数中的颜色
        if (parameter is string colorParam && !string.IsNullOrEmpty(colorParam))
        {
            var colors = colorParam.Split('|');
            if (colors.Length == 2)
            {
                var colorString = boolValue ? colors[0].Trim() : colors[1].Trim();
                try
                {
                    return Color.Parse(colorString);
                }
                catch
                {
                    // 解析失败时返回默认颜色
                }
            }
        }

        // 无参数时使用默认颜色
        return boolValue ? DefaultTrueBrush : DefaultFalseBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}