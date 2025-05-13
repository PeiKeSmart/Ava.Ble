using Avalonia.Ble.Services;
using Avalonia.Ble.Views;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Avalonia.Ble.ViewModels;

/// <summary>
/// 主窗口的视图模型。
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly BleService _bleService;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusMessage = "请点击开始扫描按钮";

    [ObservableProperty]
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

            lock (_discoveredDevices)
            {
                _discoveredDevices.Clear();
            }

            FilteredDevices.Clear();
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
        // 创建一个临时列表，避免直接修改原集合
        var tempList = new List<BleDeviceInfo>();

        // 在临时列表中应用过滤
        lock (_discoveredDevices)
        {
            if (!IsFilterEnabled || string.IsNullOrWhiteSpace(DeviceNameFilter))
            {
                // 如果未启用过滤或过滤文本为空，显示所有设备
                tempList.AddRange(_discoveredDevices);
            }
            else
            {
                // 应用过滤
                foreach (var device in _discoveredDevices)
                {
                    if (device.Name.Contains(DeviceNameFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        tempList.Add(device);
                    }
                }
            }
        }

        // 在UI线程上更新FilteredDevices集合
        Dispatcher.UIThread.Post(() =>
        {
            // 保存当前选中的设备
            BleDeviceInfo? currentSelectedDevice = SelectedDevice;
            string? selectedDeviceId = currentSelectedDevice?.Id;

            // 更新过滤后的设备列表
            FilteredDevices.Clear();
            foreach (var device in tempList)
            {
                FilteredDevices.Add(device);
            }

            // 如果之前有选中的设备，尝试在新列表中找到它并重新选中
            if (selectedDeviceId != null)
            {
                foreach (var device in FilteredDevices)
                {
                    if (device.Id == selectedDeviceId)
                    {
                        SelectedDevice = device;
                        break;
                    }
                }
            }
        });
    }

    /// <summary>
    /// 处理 DeviceDiscovered 事件。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="deviceInfo">发现的设备信息。</param>
    private void OnDeviceDiscovered(object? sender, BleDeviceInfo deviceInfo)
    {
        // 创建设备信息的副本，避免在多线程环境中修改原始对象
        var deviceInfoCopy = new BleDeviceInfo
        {
            Id = deviceInfo.Id,
            Name = deviceInfo.Name,
            Address = deviceInfo.Address,
            Rssi = deviceInfo.Rssi,
            LastSeen = deviceInfo.LastSeen,
            IsConnectable = deviceInfo.IsConnectable,
            ServiceCount = deviceInfo.ServiceCount,
            IsConnected = deviceInfo.IsConnected
        };

        // 复制广播数据
        foreach (var adData in deviceInfo.AdvertisementData)
        {
            deviceInfoCopy.AdvertisementData.Add(adData);
        }

        deviceInfoCopy.RawAdvertisementData = deviceInfo.RawAdvertisementData;

        // 使用UI线程更新集合
        Dispatcher.UIThread.Post(() =>
        {
            // 保存当前选中的设备
            BleDeviceInfo? currentSelectedDevice = SelectedDevice;
            string? selectedDeviceId = currentSelectedDevice?.Id;

            lock (_discoveredDevices)
            {
                // 检查设备是否已存在于集合中
                BleDeviceInfo? existingDevice = null;
                int existingIndex = -1;

                for (int i = 0; i < _discoveredDevices.Count; i++)
                {
                    if (_discoveredDevices[i].Id == deviceInfoCopy.Id)
                    {
                        existingDevice = _discoveredDevices[i];
                        existingIndex = i;
                        break;
                    }
                }

                if (existingDevice != null)
                {
                    // 保存当前设备的连接状态和服务信息
                    bool isConnected = existingDevice.IsConnected;
                    var services = existingDevice.Services;

                    // 检查是否是当前选中的设备
                    bool isSelectedDevice = (selectedDeviceId != null && existingDevice.Id == selectedDeviceId);

                    // 如果是选中的设备，直接更新其属性而不是替换整个对象
                    if (isSelectedDevice)
                    {
                        // 更新现有对象的属性
                        existingDevice.Rssi = deviceInfoCopy.Rssi;
                        existingDevice.LastSeen = deviceInfoCopy.LastSeen;
                        existingDevice.AdvertisementData = deviceInfoCopy.AdvertisementData;
                        existingDevice.RawAdvertisementData = deviceInfoCopy.RawAdvertisementData;
                        existingDevice.IsConnectable = deviceInfoCopy.IsConnectable;

                        // 如果名称为空或未知，则更新名称
                        if (string.IsNullOrEmpty(existingDevice.Name) || existingDevice.Name == "未知设备")
                        {
                            existingDevice.Name = deviceInfoCopy.Name;
                        }
                    }
                    else
                    {
                        // 创建更新后的设备对象
                        deviceInfoCopy.IsConnected = isConnected;
                        if (isConnected)
                        {
                            deviceInfoCopy.Services = services;
                        }

                        // 如果不是选中的设备，直接更新
                        _discoveredDevices[existingIndex] = deviceInfoCopy;
                    }
                }
                else
                {
                    // 添加新设备，但不自动选中它
                    _discoveredDevices.Add(deviceInfoCopy);
                }
            }

            // 应用过滤
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
