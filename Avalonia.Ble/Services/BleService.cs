using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;

namespace Avalonia.Ble.Services;

public class BleService {
    private BluetoothLEAdvertisementWatcher? _watcher;
    private readonly Dictionary<string, BleDeviceInfo> _deviceCache = new();
    private CancellationTokenSource? _scanCancellationTokenSource;

    public event EventHandler<BleDeviceInfo>? DeviceDiscovered;
    public event EventHandler<string>? ScanStatusChanged;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsScanning => _watcher?.Status == BluetoothLEAdvertisementWatcherStatus.Started;

    public BleService()
    {
        InitializeWatcher();
    }

    private void InitializeWatcher()
    {
        _watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };

        _watcher.Received += OnAdvertisementReceived;
        _watcher.Stopped += OnWatcherStopped;
    }

    public void StartScan()
    {
        try
        {
            if (_watcher?.Status == BluetoothLEAdvertisementWatcherStatus.Started)
            {
                ScanStatusChanged?.Invoke(this, "扫描已经在进行中");
                return;
            }

            _deviceCache.Clear();
            _scanCancellationTokenSource = new CancellationTokenSource();
            _watcher?.Start();
            ScanStatusChanged?.Invoke(this, "开始扫描");
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"启动扫描时出错: {ex.Message}");
        }
    }

    public void StopScan()
    {
        try
        {
            if (_watcher?.Status == BluetoothLEAdvertisementWatcherStatus.Started)
            {
                _watcher.Stop();
                _scanCancellationTokenSource?.Cancel();
                ScanStatusChanged?.Invoke(this, "扫描已停止");
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"停止扫描时出错: {ex.Message}");
        }
    }

    private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        try
        {
            string deviceId = args.BluetoothAddress.ToString("X");

            if (_deviceCache.ContainsKey(deviceId))
            {
                // Update existing device info
                _deviceCache[deviceId].Rssi = args.RawSignalStrengthInDBm;
                _deviceCache[deviceId].LastSeen = DateTime.Now;
                return;
            }

            // Get device name
            string deviceName = args.Advertisement.LocalName;
            if (string.IsNullOrEmpty(deviceName))
            {
                deviceName = "未知设备";
            }

            // Create new device info
            var deviceInfo = new BleDeviceInfo
            {
                Id = deviceId,
                Name = deviceName,
                Address = args.BluetoothAddress,
                Rssi = args.RawSignalStrengthInDBm,
                LastSeen = DateTime.Now
            };

            // Try to get more device information
            await GetDeviceInfoAsync(deviceInfo, args.BluetoothAddress);

            // Add to cache and notify
            _deviceCache[deviceId] = deviceInfo;
            DeviceDiscovered?.Invoke(this, deviceInfo);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"处理设备广播时出错: {ex.Message}");
        }
    }

    private async Task GetDeviceInfoAsync(BleDeviceInfo deviceInfo, ulong bluetoothAddress)
    {
        try
        {
            if (_scanCancellationTokenSource?.Token.IsCancellationRequested == true)
                return;

            string deviceSelector = $"System.Devices.Aep.BluetoothAddress:=\"{bluetoothAddress}\"";
            var devices = await DeviceInformation.FindAllAsync(deviceSelector);

            if (devices.Count > 0)
            {
                var device = await BluetoothLEDevice.FromIdAsync(devices[0].Id);
                if (device != null)
                {
                    deviceInfo.Name = string.IsNullOrEmpty(device.Name) ? deviceInfo.Name : device.Name;
                    deviceInfo.IsConnectable = true;

                    // Get services if connected
                    if (device.ConnectionStatus == BluetoothConnectionStatus.Connected)
                    {
                        var services = await device.GetGattServicesAsync();
                        if (services.Status == Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success)
                        {
                            deviceInfo.ServiceCount = services.Services.Count;
                        }
                    }

                    device.Dispose();
                }
            }
        }
        catch (Exception)
        {
            // Silently fail - this is just additional info
        }
    }

    private void OnWatcherStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        if (args.Error != BluetoothError.Success)
        {
            ErrorOccurred?.Invoke(this, $"扫描意外停止: {args.Error}");
        }
    }
}

public class BleDeviceInfo {
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ulong Address { get; set; }
    public short Rssi { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsConnectable { get; set; }
    public int ServiceCount { get; set; }

    public string DisplayName => string.IsNullOrEmpty(Name) ? Id : $"{Name} ({Id})";
    public string SignalStrength => $"{Rssi} dBm";
    public string LastSeenTime => LastSeen.ToString("HH:mm:ss");
}