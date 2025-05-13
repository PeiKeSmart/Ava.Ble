using Avalonia.Ble.Services;

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
    private ObservableCollection<object> _discoveredDevices = []; // 使用 object 作为占位符

    [ObservableProperty]
    private object? _selectedDevice;

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
        IsScanning = true;
        StatusMessage = "正在扫描设备...";
        // 在这里添加开始扫描的逻辑
        DiscoveredDevices.Clear();
        _bleService.StartScan();
        IsScanning = true;
    }

    [RelayCommand(CanExecute = nameof(CanStopScan))]
    private void StopScan()
    {
        IsScanning = false;
        StatusMessage = "扫描已停止。";
        // 在这里添加停止扫描的逻辑
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

    }

    private void OnScanStatusChanged(object? sender, string status)
    {

    }

    private void OnErrorOccurred(object? sender, string errorMessage)
    {

    }
}
