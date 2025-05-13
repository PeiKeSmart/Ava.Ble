using Avalonia.Ble.Services;
using Avalonia.Ble.Views;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Avalonia.Ble.ViewModels;

/// <summary>
/// 主窗口的视图模型。
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly BleService _bleService;

    // 保存DataGrid的滚动位置
    private double _scrollPosition = 0;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusMessage = "请点击开始扫描按钮";

    private ObservableCollection<BleDeviceInfo> _discoveredDevices = [];

    /// <summary>
    /// 获取或设置过滤后的设备列表。
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<BleDeviceInfo> _filteredDevices = [];

    /// <summary>
    /// 获取或设置设备名称过滤文本。
    /// </summary>
    [ObservableProperty]
    private string _deviceNameFilter = string.Empty;

    /// <summary>
    /// 获取或设置是否启用过滤。
    /// </summary>
    [ObservableProperty]
    private bool _isFilterEnabled = false;

    /// <summary>
    /// 获取过滤按钮文本。
    /// </summary>
    public string FilterButtonText => IsFilterEnabled ? "关闭过滤" : "开启过滤";

    [ObservableProperty]
    private BleDeviceInfo? _selectedDevice;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private ObservableCollection<BleServiceInfo> _selectedDeviceServices = [];

    [ObservableProperty]
    private BleServiceInfo? _selectedService;

    [ObservableProperty]
    private ObservableCollection<BleCharacteristicInfo> _selectedServiceCharacteristics = [];

    [ObservableProperty]
    private BleCharacteristicInfo? _selectedCharacteristic;

    /// <summary>
    /// 初始化 MainWindowViewModel 类的新实例。
    /// </summary>
    public MainWindowViewModel()
    {
        _bleService = new BleService();
        _bleService.DeviceDiscovered += OnDeviceDiscovered;
        _bleService.ScanStatusChanged += OnScanStatusChanged;
        _bleService.ErrorOccurred += OnErrorOccurred;
        _bleService.DeviceConnected += OnDeviceConnected;
        _bleService.DeviceDisconnected += OnDeviceDisconnected;
        _bleService.ServiceDiscovered += OnServiceDiscovered;

        // 初始化过滤后的设备列表
        ApplyFilter();
    }

    /// <summary>
    /// 当选定设备更改时调用。
    /// </summary>
    /// <param name="value">新的选定设备。</param>
    partial void OnSelectedDeviceChanged(BleDeviceInfo? value)
    {
        if (value != null)
        {
            // 当选择设备变化时，更新服务列表
            SelectedDeviceServices.Clear();
            foreach (var service in value.Services)
            {
                SelectedDeviceServices.Add(service);
            }
        }
        else
        {
            SelectedDeviceServices.Clear();
        }

        // 更新连接命令的可用性
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 当选定服务更改时调用。
    /// </summary>
    /// <param name="value">新的选定服务。</param>
    partial void OnSelectedServiceChanged(BleServiceInfo? value)
    {
        if (value != null)
        {
            // 当选择服务变化时，更新特征列表
            SelectedServiceCharacteristics.Clear();
            foreach (var characteristic in value.Characteristics)
            {
                SelectedServiceCharacteristics.Add(characteristic);
            }
        }
        else
        {
            SelectedServiceCharacteristics.Clear();
        }
    }

    /// <summary>
    /// 开始扫描 BLE 设备。
    /// </summary>
    [RelayCommand]
    private void StartScan()
    {
        StatusMessage = "正在扫描设备...";

        // 在UI线程上清空集合
        Dispatcher.UIThread.Post(() =>
        {
            // 先清空选中的设备，避免引用已删除的对象
            SelectedDevice = null;

            // 创建新的空集合替换原集合
            lock (_discoveredDevices)
            {
                _discoveredDevices = new ObservableCollection<BleDeviceInfo>();
            }

            // 创建新的空集合替换过滤后的集合
            FilteredDevices = new ObservableCollection<BleDeviceInfo>();
        });

        _bleService.StartScan();
        IsScanning = true;
    }

    /// <summary>
    /// 停止扫描 BLE 设备。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStopScan))]
    private void StopScan()
    {
        StatusMessage = "扫描已停止。";
        _bleService.StopScan();
        IsScanning = false;
    }

    /// <summary>
    /// 确定是否可以停止扫描。
    /// </summary>
    /// <returns>如果正在扫描，则为 true；否则为 false。</returns>
    private bool CanStopScan() => IsScanning;

    /// <summary>
    /// 当 IsScanning 属性更改时调用。
    /// </summary>
    /// <param name="value">IsScanning 的新值。</param>
    partial void OnIsScanningChanged(bool value)
    {
        StopScanCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 当设备名称过滤文本更改时调用。
    /// </summary>
    /// <param name="value">新的过滤文本。</param>
    partial void OnDeviceNameFilterChanged(string value)
    {
        if (IsFilterEnabled)
        {
            ApplyFilter();
        }
    }

    /// <summary>
    /// 当是否启用过滤更改时调用。
    /// </summary>
    /// <param name="value">新的启用状态。</param>
    partial void OnIsFilterEnabledChanged(bool value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(FilterButtonText));
    }

    /// <summary>
    /// 应用设备过滤。
    /// </summary>
    private void ApplyFilter()
    {
        string? selectedDeviceId = SelectedDevice?.Id; // 保留选择

        List<BleDeviceInfo> sourceListForFilter;
        lock (_discoveredDevices) // 安全地迭代 _discoveredDevices
        {
            sourceListForFilter = new List<BleDeviceInfo>(_discoveredDevices); // 使用快照进行操作
        }

        List<BleDeviceInfo> devicesThatShouldBeInFilteredView = new List<BleDeviceInfo>();
        if (!IsFilterEnabled || string.IsNullOrWhiteSpace(DeviceNameFilter))
        {
            devicesThatShouldBeInFilteredView.AddRange(sourceListForFilter);
        }
        else
        {
            string filterText = DeviceNameFilter.Trim();
            foreach (var device in sourceListForFilter)
            {
                bool nameMatches = device.Name != null && device.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase);
                // 如果设备名称为空或“未知设备”，并且过滤器文本为“未知设备”，则匹配
                bool isUnknownDeviceMatch = (string.IsNullOrEmpty(device.Name) || device.Name == "未知设备") &&
                                            filterText.Equals("未知设备", StringComparison.OrdinalIgnoreCase);
                
                if (nameMatches || isUnknownDeviceMatch)
                {
                    devicesThatShouldBeInFilteredView.Add(device);
                }
            }
        }

        // 将 FilteredDevices 与 devicesThatShouldBeInFilteredView 同步
        // 这假设 devicesThatShouldBeInFilteredView 包含所需的顺序。

        // 1. 从 FilteredDevices 中移除不再存在于 devicesThatShouldBeInFilteredView 中的项
        var itemsToRemove = FilteredDevices.Except(devicesThatShouldBeInFilteredView).ToList();
        foreach (var itemToRemove in itemsToRemove)
        {
            FilteredDevices.Remove(itemToRemove);
        }

        // 2. 添加/移动项以匹配 devicesThatShouldBeInFilteredView 的顺序和内容
        for (int i = 0; i < devicesThatShouldBeInFilteredView.Count; i++)
        {
            var itemFromSource = devicesThatShouldBeInFilteredView[i];
            if (i >= FilteredDevices.Count)
            {
                // 需要在末尾添加项
                FilteredDevices.Add(itemFromSource);
            }
            else if (!ReferenceEquals(FilteredDevices[i], itemFromSource))
            {
                // 此位置的项不同。检查 itemFromSource 是否已存在于 FilteredDevices 中的其他位置。
                int existingIndexOfItemFromSource = -1;
                for(int j = 0; j < FilteredDevices.Count; ++j) {
                    if(ReferenceEquals(FilteredDevices[j], itemFromSource)) {
                        existingIndexOfItemFromSource = j;
                        break;
                    }
                }

                if (existingIndexOfItemFromSource != -1)
                {
                    // 项存在于 FilteredDevices 中的其他位置，将其移动到正确位置
                    FilteredDevices.Move(existingIndexOfItemFromSource, i);
                }
                else
                {
                    // 项根本不在 FilteredDevices 中，插入它
                    FilteredDevices.Insert(i, itemFromSource);
                }
            }
            // 如果 FilteredDevices[i] 已经是 itemFromSource，则它位于正确的位置。
        }

        // 3. 如果在插入/移动后 FilteredDevices 比 devicesThatShouldBeInFilteredView 长，则修剪多余的项。
        while (FilteredDevices.Count > devicesThatShouldBeInFilteredView.Count)
        {
            FilteredDevices.RemoveAt(FilteredDevices.Count - 1);
        }

        // 如果可能，恢复选择
        if (selectedDeviceId != null)
        {
            SelectedDevice = FilteredDevices.FirstOrDefault(d => d.Id == selectedDeviceId);
        }
        else
        {
            SelectedDevice = null; // 或者根据需要选择第一项
        }
        // 不再需要恢复滚动位置的注释，因为正确的集合管理应该能解决此问题。
    }

    /// <summary>
    /// 处理 DeviceDiscovered 事件。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="deviceInfoFromEvent">发现的设备信息。</param>
    private void OnDeviceDiscovered(object? sender, BleDeviceInfo deviceInfoFromEvent)
    {
        Dispatcher.UIThread.Post(() =>
        {
            BleDeviceInfo? existingDeviceInMasterList;
            lock (_discoveredDevices) // _discoveredDevices is ObservableCollection<BleDeviceInfo>
            {
                existingDeviceInMasterList = _discoveredDevices.FirstOrDefault(d => d.Id == deviceInfoFromEvent.Id);

                if (existingDeviceInMasterList != null)
                {
                    // Update existing instance's properties
                    string newNameFromEvent = deviceInfoFromEvent.Name;

                    if (!string.IsNullOrEmpty(newNameFromEvent) && newNameFromEvent != "未知设备")
                    {
                        // Event brings a valid, specific name. Update if different.
                        if (existingDeviceInMasterList.Name != newNameFromEvent)
                        {
                             existingDeviceInMasterList.Name = newNameFromEvent;
                        }
                    }
                    else if (string.IsNullOrEmpty(existingDeviceInMasterList.Name) || existingDeviceInMasterList.Name == "未知设备")
                    {
                        // Event name is non-specific (null, empty, or "未知设备")
                        // AND current name is also non-specific (null, empty, or "未知设备").
                        // Ensure it's "未知设备". Avoid redundant assignments if already "未知设备".
                        if (existingDeviceInMasterList.Name != "未知设备") 
                        {
                             existingDeviceInMasterList.Name = "未知设备";
                        }
                    }
                    // If event name is non-specific but current name is specific, do nothing (preserve the good name).

                    existingDeviceInMasterList.Rssi = deviceInfoFromEvent.Rssi;
                    existingDeviceInMasterList.LastSeen = deviceInfoFromEvent.LastSeen;
                    existingDeviceInMasterList.IsConnectable = deviceInfoFromEvent.IsConnectable;
                    
                    // BleService should provide consolidated AdvertisementData.
                    // BleDeviceInfo.AdvertisementData setter calls OnPropertyChanged.
                    existingDeviceInMasterList.AdvertisementData = new List<BleAdvertisementData>(deviceInfoFromEvent.AdvertisementData);
                    existingDeviceInMasterList.RawAdvertisementData = deviceInfoFromEvent.RawAdvertisementData;
                    // ServiceCount and IsConnected are typically updated upon connection.
                }
                else
                {
                    // Device not found, add new BleDeviceInfo instance.
                    // Ensure name is "未知设备" if not a valid specific name from the event.
                    var newDeviceToAdd = new BleDeviceInfo
                    {
                        Id = deviceInfoFromEvent.Id,
                        Name = (!string.IsNullOrEmpty(deviceInfoFromEvent.Name) && deviceInfoFromEvent.Name != "未知设备") ? deviceInfoFromEvent.Name : "未知设备",
                        Address = deviceInfoFromEvent.Address,
                        Rssi = deviceInfoFromEvent.Rssi,
                        LastSeen = deviceInfoFromEvent.LastSeen,
                        IsConnectable = deviceInfoFromEvent.IsConnectable,
                        ServiceCount = deviceInfoFromEvent.ServiceCount,
                        IsConnected = deviceInfoFromEvent.IsConnected,
                        AdvertisementData = new List<BleAdvertisementData>(deviceInfoFromEvent.AdvertisementData),
                        RawAdvertisementData = deviceInfoFromEvent.RawAdvertisementData
                        // Services list is initially empty
                    };
                    _discoveredDevices.Add(newDeviceToAdd);
                }
            }
            // _discoveredDevices has been updated in-place (item added or properties of existing item changed).
            // ApplyFilter will use these stable instances.
            ApplyFilter();
        });
    }

    /// <summary>
    /// 处理 ScanStatusChanged 事件。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="status">新的扫描状态。</param>
    private void OnScanStatusChanged(object? sender, string status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = status;
        });
    }

    /// <summary>
    /// 处理 ErrorOccurred 事件。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="errorMessage">错误消息。</param>
    private void OnErrorOccurred(object? sender, string errorMessage)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = $"错误: {errorMessage}";
        });
    }

    /// <summary>
    /// 处理 DeviceConnected 事件。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="deviceInfo">连接的设备信息。</param>
    private void OnDeviceConnected(object? sender, BleDeviceInfo deviceInfo)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // 更新UI
            IsConnecting = false;

            // 如果连接的设备是当前选中的设备，更新服务列表
            if (SelectedDevice != null && SelectedDevice.Id == deviceInfo.Id)
            {
                SelectedDeviceServices.Clear();
                foreach (var service in deviceInfo.Services)
                {
                    SelectedDeviceServices.Add(service);
                }
            }

            // 更新命令状态
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        });
    }

    /// <summary>
    /// 处理 DeviceDisconnected 事件。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="deviceInfo">断开连接的设备信息。</param>
    private void OnDeviceDisconnected(object? sender, BleDeviceInfo deviceInfo)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // 更新UI
            if (SelectedDevice != null && SelectedDevice.Id == deviceInfo.Id)
            {
                SelectedDeviceServices.Clear();
            }

            // 更新命令状态
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        });
    }

    /// <summary>
    /// 处理 ServiceDiscovered 事件。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="serviceInfo">发现的服务信息。</param>
    private void OnServiceDiscovered(object? sender, BleServiceInfo serviceInfo)
    {
        // 这个方法在连接过程中会被多次调用，但我们已经在OnDeviceConnected中更新了服务列表
        // 所以这里不需要额外处理
    }

    /// <summary>
    /// 异步连接到选定的 BLE 设备。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (SelectedDevice == null) return;

        try
        {
            IsConnecting = true;
            StatusMessage = $"正在连接到设备: {SelectedDevice.Name}...";

            bool result = await _bleService.ConnectToDeviceAsync(SelectedDevice);

            if (!result)
            {
                StatusMessage = $"连接设备失败: {SelectedDevice.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"连接时出错: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
        }
    }

    /// <summary>
    /// 确定是否可以连接到选定的设备。
    /// </summary>
    /// <returns>如果可以连接，则为 true；否则为 false。</returns>
    private bool CanConnect()
    {
        return SelectedDevice != null &&
               SelectedDevice.IsConnectable &&
               !SelectedDevice.IsConnected &&
               !IsConnecting;
    }

    /// <summary>
    /// 断开与选定 BLE 设备的连接。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private void Disconnect()
    {
        if (SelectedDevice == null || !SelectedDevice.IsConnected) return;

        _bleService.DisconnectDevice(SelectedDevice);
    }

    /// <summary>
    /// 确定是否可以断开与选定设备的连接。
    /// </summary>
    /// <returns>如果可以断开连接，则为 true；否则为 false。</returns>
    private bool CanDisconnect()
    {
        return SelectedDevice != null && SelectedDevice.IsConnected;
    }

    /// <summary>
    /// 切换过滤状态。
    /// </summary>
    [RelayCommand]
    private void ToggleFilter()
    {
        IsFilterEnabled = !IsFilterEnabled;
    }

    /// <summary>
    /// 保存DataGrid的滚动位置。
    /// </summary>
    /// <param name="scrollViewer">ScrollViewer控件。</param>
    public void SaveScrollPosition(object scrollViewer)
    {
        if (scrollViewer is Avalonia.Controls.ScrollViewer viewer)
        {
            _scrollPosition = viewer.Offset.Y;
        }
    }

    /// <summary>
    /// 恢复DataGrid的滚动位置。
    /// </summary>
    /// <param name="scrollViewer">ScrollViewer控件。</param>
    public void RestoreScrollPosition(object scrollViewer)
    {
        if (scrollViewer is Avalonia.Controls.ScrollViewer viewer)
        {
            viewer.Offset = new Avalonia.Vector(viewer.Offset.X, _scrollPosition);
        }
    }

    /// <summary>
    /// 查看设备的广播数据。
    /// </summary>
    /// <param name="parameter">设备参数，如果为null则使用当前选中的设备。</param>
    [RelayCommand]
    private void ViewAdvertisementData(object? parameter)
    {
        // 获取设备信息
        BleDeviceInfo? device = parameter as BleDeviceInfo ?? SelectedDevice;
        if (device == null) return;

        // 检查设备是否有广播数据
        if (device.AdvertisementData == null || device.AdvertisementData.Count == 0)
        {
            // 如果没有广播数据，显示提示信息
            StatusMessage = "该设备没有广播数据";
            return;
        }

        // 创建并显示广播数据窗口
        var window = new AdvertisementDataWindow(device);
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            window.ShowDialog(desktop.MainWindow!);
        }
    }
}
