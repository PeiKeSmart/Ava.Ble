using System;
using System.Collections.ObjectModel;
using Ava.Ble.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ava.Ble.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly BleService _bleService;

    // 使用手动实现的属性以确保AOT兼容性
    private string _statusMessage = "准备就绪";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    private ObservableCollection<BleDeviceInfo> _discoveredDevices = new();
    public ObservableCollection<BleDeviceInfo> DiscoveredDevices
    {
        get => _discoveredDevices;
        set => SetProperty(ref _discoveredDevices, value);
    }

    private BleDeviceInfo? _selectedDevice;
    public BleDeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set => SetProperty(ref _selectedDevice, value);
    }

    public IRelayCommand StartScanCommand { get; }
    public IRelayCommand StopScanCommand { get; }

    public MainWindowViewModel()
    {
        _bleService = new BleService();
        _bleService.DeviceDiscovered += OnDeviceDiscovered;
        _bleService.ScanStatusChanged += OnScanStatusChanged;
        _bleService.ErrorOccurred += OnErrorOccurred;

        // 初始化命令
        StartScanCommand = new RelayCommand(StartScan);
        StopScanCommand = new RelayCommand(StopScan);
    }

    private void StartScan()
    {
        DiscoveredDevices.Clear();
        _bleService.StartScan();
        IsScanning = true;
    }

    private void StopScan()
    {
        _bleService.StopScan();
        IsScanning = false;
    }

    private void OnDeviceDiscovered(object? sender, BleDeviceInfo deviceInfo)
    {
        // We need to use the UI thread to update the collection
        Dispatcher.UIThread.Post(() =>
        {
            // Check if device already exists in the collection
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
                // Update existing device
                int index = DiscoveredDevices.IndexOf(existingDevice);
                DiscoveredDevices[index] = deviceInfo;
            }
            else
            {
                // Add new device
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
