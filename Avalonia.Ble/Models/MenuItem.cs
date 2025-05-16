using System;

namespace Avalonia.Ble.Models;

/// <summary>
/// 表示应用菜单项。
/// </summary>
public class AppMenuItem
{
    /// <summary>
    /// 获取或设置菜单项的名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置菜单项的图标。
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置菜单项的类型。
    /// </summary>
    public MenuItemType Type { get; set; }
}

/// <summary>
/// 表示菜单项类型。
/// </summary>
public enum MenuItemType
{
    /// <summary>
    /// 设备扫描页面。
    /// </summary>
    DeviceScan,

    /// <summary>
    /// 设置页面。
    /// </summary>
    Settings,

    /// <summary>
    /// 关于页面。
    /// </summary>
    About
}
