using Avalonia.Ble.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System;
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
        DiscoveredDevices.Clear();
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
    /// 处理 DeviceDiscovered 事件。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="deviceInfo">发现的设备信息。</param>
    private void OnDeviceDiscovered(object? sender, BleDeviceInfo deviceInfo)
    {
        // 使用UI线程更新集合
        Dispatcher.UIThread.Post(() =>
        {
            // 检查设备是否已存在于集合中
            BleDeviceInfo? existingDevice = null;
            foreach (var device in DiscoveredDevices)
            {
                if (device.Id == deviceInfo.Id)
                {
                    existingDevice = device;
                    break;
                }
            }

            if (existingDevice != null)
            {
                // 更新现有设备
                int index = DiscoveredDevices.IndexOf(existingDevice);
                DiscoveredDevices[index] = deviceInfo;
            }
            else
            {
                // 添加新设备
                DiscoveredDevices.Add(deviceInfo);
            }
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
}
