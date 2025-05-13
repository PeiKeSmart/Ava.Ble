using Avalonia.Ble.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Avalonia.Ble.ViewModels;

public partial class MainWindowViewModel : ViewModelBase // Changed from ViewModelBase
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

    [RelayCommand]
    private void StartScan()
    {
        StatusMessage = "正在扫描设备...";
        DiscoveredDevices.Clear();
        _bleService.StartScan();
        IsScanning = true;
    }

    [RelayCommand(CanExecute = nameof(CanStopScan))]
    private void StopScan()
    {
        StatusMessage = "扫描已停止。";
        _bleService.StopScan();
        IsScanning = false;
    }

    private bool CanStopScan() => IsScanning;

    // 当 IsScanning 属性改变时，通知 StopScanCommand 的 CanExecute 状态也可能改变
    partial void OnIsScanningChanged(bool value)
    {
        StopScanCommand.NotifyCanExecuteChanged();
    }

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

    private void OnScanStatusChanged(object? sender, string status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = status;
        });
    }

    private void OnErrorOccurred(object? sender, string errorMessage)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = $"错误: {errorMessage}";
        });
    }

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

    private void OnServiceDiscovered(object? sender, BleServiceInfo serviceInfo)
    {
        // 这个方法在连接过程中会被多次调用，但我们已经在OnDeviceConnected中更新了服务列表
        // 所以这里不需要额外处理
    }

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

    private bool CanConnect()
    {
        return SelectedDevice != null &&
               SelectedDevice.IsConnectable &&
               !SelectedDevice.IsConnected &&
               !IsConnecting;
    }

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private void Disconnect()
    {
        if (SelectedDevice == null || !SelectedDevice.IsConnected) return;

        _bleService.DisconnectDevice(SelectedDevice);
    }

    private bool CanDisconnect()
    {
        return SelectedDevice != null && SelectedDevice.IsConnected;
    }
}
