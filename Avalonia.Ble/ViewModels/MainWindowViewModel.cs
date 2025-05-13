using Avalonia.Ble.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Collections.ObjectModel;

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

    public MainWindowViewModel()
    {
        _bleService = new BleService();
        _bleService.DeviceDiscovered += OnDeviceDiscovered;
        _bleService.ScanStatusChanged += OnScanStatusChanged;
        _bleService.ErrorOccurred += OnErrorOccurred;
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
}
