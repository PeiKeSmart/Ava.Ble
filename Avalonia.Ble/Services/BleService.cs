using NewLife.Log;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace Avalonia.Ble.Services;

public class BleService {
    private BluetoothLEAdvertisementWatcher? _watcher;
    private readonly Dictionary<string, BleDeviceInfo> _deviceCache = new();
    private CancellationTokenSource? _scanCancellationTokenSource;

    public event EventHandler<BleDeviceInfo>? DeviceDiscovered;
    public event EventHandler<string>? ScanStatusChanged;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<BleDeviceInfo>? DeviceConnected;
    public event EventHandler<BleDeviceInfo>? DeviceDisconnected;
    public event EventHandler<BleServiceInfo>? ServiceDiscovered;

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

            var device = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
            if (device != null)
            {
                XTrace.WriteLine($"获取到的蓝牙：{device.Name}");
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
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error in GetDeviceInfoAsync for {bluetoothAddress:X}: {ex.Message}");
            XTrace.WriteLine($"Error in GetDeviceInfoAsync for {bluetoothAddress:X}: {ex.Message}");
        }
    }

    private void OnWatcherStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        if (args.Error != BluetoothError.Success)
        {
            ErrorOccurred?.Invoke(this, $"扫描意外停止: {args.Error}");
            XTrace.WriteLine($"扫描意外停止: {args.Error}");
        }
    }

    // 连接到设备
    public async Task<bool> ConnectToDeviceAsync(BleDeviceInfo deviceInfo)
    {
        try
        {
            ScanStatusChanged?.Invoke(this, $"正在连接到设备: {deviceInfo.Name}...");

            var device = await BluetoothLEDevice.FromBluetoothAddressAsync(deviceInfo.Address);
            if (device == null)
            {
                ErrorOccurred?.Invoke(this, $"无法连接到设备: {deviceInfo.Name}");
                return false;
            }

            // 清除之前的服务列表
            deviceInfo.Services.Clear();

            // 获取服务
            var servicesResult = await device.GetGattServicesAsync();
            if (servicesResult.Status != GattCommunicationStatus.Success)
            {
                ErrorOccurred?.Invoke(this, $"获取服务失败: {servicesResult.Status}");
                device.Dispose();
                return false;
            }

            deviceInfo.ServiceCount = servicesResult.Services.Count;

            // 处理每个服务
            foreach (var service in servicesResult.Services)
            {
                var serviceInfo = new BleServiceInfo
                {
                    Id = service.AttributeHandle.ToString(),
                    Uuid = service.Uuid.ToString(),
                    Name = GetServiceName(service.Uuid)
                };

                // 获取特征
                var characteristicsResult = await service.GetCharacteristicsAsync();
                if (characteristicsResult.Status == GattCommunicationStatus.Success)
                {
                    foreach (var characteristic in characteristicsResult.Characteristics)
                    {
                        var characteristicInfo = new BleCharacteristicInfo
                        {
                            Id = characteristic.AttributeHandle.ToString(),
                            Uuid = characteristic.Uuid.ToString(),
                            Name = GetCharacteristicName(characteristic.Uuid),
                            CanRead = characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read),
                            CanWrite = characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write) ||
                                      characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse),
                            CanNotify = characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) ||
                                       characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate)
                        };

                        // 如果可读，尝试读取值
                        if (characteristicInfo.CanRead)
                        {
                            try
                            {
                                var readResult = await characteristic.ReadValueAsync();
                                if (readResult.Status == GattCommunicationStatus.Success)
                                {
                                    var reader = Windows.Storage.Streams.DataReader.FromBuffer(readResult.Value);
                                    byte[] data = new byte[readResult.Value.Length];
                                    reader.ReadBytes(data);
                                    characteristicInfo.Value = BitConverter.ToString(data).Replace("-", " ");
                                }
                            }
                            catch (Exception ex)
                            {
                                XTrace.WriteLine($"读取特征值时出错: {ex.Message}");
                            }
                        }

                        serviceInfo.Characteristics.Add(characteristicInfo);
                    }
                }

                deviceInfo.Services.Add(serviceInfo);
                ServiceDiscovered?.Invoke(this, serviceInfo);
            }

            deviceInfo.IsConnected = true;
            DeviceConnected?.Invoke(this, deviceInfo);
            ScanStatusChanged?.Invoke(this, $"已连接到设备: {deviceInfo.Name}");

            // 释放设备对象
            device.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"连接设备时出错: {ex.Message}");
            XTrace.WriteLine($"连接设备时出错: {ex.Message}");
            return false;
        }
    }

    // 断开连接
    public void DisconnectDevice(BleDeviceInfo deviceInfo)
    {
        try
        {
            // 在BLE中，断开连接通常只是停止与设备的交互
            deviceInfo.IsConnected = false;
            DeviceDisconnected?.Invoke(this, deviceInfo);
            ScanStatusChanged?.Invoke(this, $"已断开与设备的连接: {deviceInfo.Name}");
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"断开连接时出错: {ex.Message}");
            XTrace.WriteLine($"断开连接时出错: {ex.Message}");
        }
    }

    // 获取服务名称（可以根据标准UUID添加更多已知服务）
    private string GetServiceName(Guid uuid)
    {
        switch (uuid.ToString().ToUpper())
        {
            case "00001800-0000-1000-8000-00805F9B34FB": return "通用访问";
            case "00001801-0000-1000-8000-00805F9B34FB": return "通用属性";
            case "0000180A-0000-1000-8000-00805F9B34FB": return "设备信息";
            case "0000180F-0000-1000-8000-00805F9B34FB": return "电池服务";
            case "00001809-0000-1000-8000-00805F9B34FB": return "健康温度计";
            case "0000180D-0000-1000-8000-00805F9B34FB": return "心率";
            default: return string.Empty;
        }
    }

    // 获取特征名称（可以根据标准UUID添加更多已知特征）
    private string GetCharacteristicName(Guid uuid)
    {
        switch (uuid.ToString().ToUpper())
        {
            case "00002A00-0000-1000-8000-00805F9B34FB": return "设备名称";
            case "00002A01-0000-1000-8000-00805F9B34FB": return "外观";
            case "00002A19-0000-1000-8000-00805F9B34FB": return "电池电量";
            case "00002A1C-0000-1000-8000-00805F9B34FB": return "温度测量";
            case "00002A29-0000-1000-8000-00805F9B34FB": return "制造商名称";
            case "00002A24-0000-1000-8000-00805F9B34FB": return "型号";
            case "00002A25-0000-1000-8000-00805F9B34FB": return "序列号";
            case "00002A27-0000-1000-8000-00805F9B34FB": return "硬件版本";
            case "00002A26-0000-1000-8000-00805F9B34FB": return "固件版本";
            case "00002A28-0000-1000-8000-00805F9B34FB": return "软件版本";
            default: return string.Empty;
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
    public bool IsConnected { get; set; }
    public List<BleServiceInfo> Services { get; set; } = new List<BleServiceInfo>();
    public string ConnectionStatus => IsConnected ? "已连接" : "未连接";

    public string DisplayName => string.IsNullOrEmpty(Name) ? Id : $"{Name} ({Id})";
    public string SignalStrength => $"{Rssi} dBm";
    public string LastSeenTime => LastSeen.ToString("HH:mm:ss");
}

public class BleServiceInfo {
    public string Id { get; set; } = string.Empty;
    public string Uuid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<BleCharacteristicInfo> Characteristics { get; set; } = new List<BleCharacteristicInfo>();

    public string DisplayName => string.IsNullOrEmpty(Name) ? Uuid : $"{Name} ({Uuid})";
}

public class BleCharacteristicInfo {
    public string Id { get; set; } = string.Empty;
    public string Uuid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanNotify { get; set; }
    public string Value { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrEmpty(Name) ? Uuid : $"{Name} ({Uuid})";
    public string Properties => $"{(CanRead ? "读 " : "")}{(CanWrite ? "写 " : "")}{(CanNotify ? "通知" : "")}";
}