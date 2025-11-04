using Avalonia.Ble.Models;
using Avalonia.Ble.Views;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Avalonia.Ble.Converters;

/// <summary>
/// 将菜单项类型转换为对应的页面。
/// </summary>
public class MenuItemToPageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not MenuItemType menuItemType)
        {
            return new DeviceScanPage(); // 默认返回设备扫描页面
        }

        return menuItemType switch
        {
            MenuItemType.DeviceScan => new DeviceScanPage(),
            MenuItemType.Settings => new SettingsPage(),
            MenuItemType.About => new AboutPage(),
            _ => new DeviceScanPage()
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
