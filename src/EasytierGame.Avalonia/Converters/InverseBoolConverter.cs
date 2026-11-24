using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace EasytierGame.Desktop.Converters;

/// <summary>bool 取反。用于“开启/关闭”互斥绑定。</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;
}
