using NewLife.Log;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace Avalonia.Ble.Services;

/// <summary>
/// 提供蓝牙低功耗 (BLE) 服务。
/// </summary>
public class BleService {
    private BluetoothLEAdvertisementWatcher? _watcher;
    private readonly Dictionary<string, BleDeviceInfo> _deviceCache = new();
    private CancellationTokenSource? _scanCancellationTokenSource;

    /// <summary>
    /// 当发现新设备时触发。
    /// </summary>
    public event EventHandler<BleDeviceInfo>? DeviceDiscovered;
    /// <summary>
    /// 当扫描状态改变时触发。
    /// </summary>
    public event EventHandler<string>? ScanStatusChanged;
    /// <summary>
    /// 当发生错误时触发。
    /// </summary>
    public event EventHandler<string>? ErrorOccurred;
    /// <summary>
    /// 当设备连接时触发。
    /// </summary>
    public event EventHandler<BleDeviceInfo>? DeviceConnected;
    /// <summary>
    /// 当设备断开连接时触发。
    /// </summary>
    public event EventHandler<BleDeviceInfo>? DeviceDisconnected;
    /// <summary>
    /// 当发现服务时触发。
    /// </summary>
    public event EventHandler<BleServiceInfo>? ServiceDiscovered;

    /// <summary>
    /// 获取一个值，该值指示当前是否正在扫描设备。
    /// </summary>
    public bool IsScanning => _watcher?.Status == BluetoothLEAdvertisementWatcherStatus.Started;

    /// <summary>
    /// 初始化 BleService 类的新实例。
    /// </summary>
    public BleService()
    {
        InitializeWatcher();
    }

    /// <summary>
    /// 初始化蓝牙 LE 广播观察者。
    /// </summary>
    private void InitializeWatcher()
    {
        _watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };

        _watcher.Received += OnAdvertisementReceived;
        _watcher.Stopped += OnWatcherStopped;
    }

    /// <summary>
    /// 开始扫描 BLE 设备。
    /// </summary>
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

    /// <summary>
    /// 停止扫描 BLE 设备。
    /// </summary>
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

    /// <summary>
    /// 处理接收到的蓝牙 LE 广播。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">事件参数。</param>
    private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        try
        {
            string deviceId = args.BluetoothAddress.ToString("X");

            // 获取设备名称
            string deviceName = args.Advertisement.LocalName;
            if (string.IsNullOrEmpty(deviceName))
            {
                deviceName = "未知设备";
            }

            // 处理广播数据
            List<BleAdvertisementData> advertisementDataList = new List<BleAdvertisementData>();
            string rawData = string.Empty;

            // 获取原始广播数据
            if (args.Advertisement.DataSections != null && args.Advertisement.DataSections.Count > 0)
            {
                foreach (var section in args.Advertisement.DataSections)
                {
                    var reader = Windows.Storage.Streams.DataReader.FromBuffer(section.Data);
                    byte[] data = new byte[section.Data.Length];
                    reader.ReadBytes(data);

                    var adData = new BleAdvertisementData
                    {
                        Length = (byte)(data.Length + 1), // +1 for type
                        Type = section.DataType,
                        Value = data
                    };

                    advertisementDataList.Add(adData);

                    // 添加到原始数据字符串
                    rawData += $"{adData.Length:X2} {adData.Type:X2} {BitConverter.ToString(adData.Value).Replace("-", " ")} ";
                }
            }

            // 如果设备已存在，更新信息
            if (_deviceCache.ContainsKey(deviceId))
            {
                // 更新现有设备信息
                _deviceCache[deviceId].Rssi = args.RawSignalStrengthInDBm;
                _deviceCache[deviceId].LastSeen = DateTime.Now;
                _deviceCache[deviceId].AdvertisementData = advertisementDataList;
                _deviceCache[deviceId].RawAdvertisementData = rawData.Trim();

                // 通知设备更新
                DeviceDiscovered?.Invoke(this, _deviceCache[deviceId]);
                return;
            }

            // 创建新的设备信息
            var deviceInfo = new BleDeviceInfo
            {
                Id = deviceId,
                Name = deviceName,
                Address = args.BluetoothAddress,
                Rssi = args.RawSignalStrengthInDBm,
                LastSeen = DateTime.Now,
                AdvertisementData = advertisementDataList,
                RawAdvertisementData = rawData.Trim()
            };

            // 尝试获取更多设备信息
            await GetDeviceInfoAsync(deviceInfo, args.BluetoothAddress);

            // 添加到缓存并通知
            _deviceCache[deviceId] = deviceInfo;
            DeviceDiscovered?.Invoke(this, deviceInfo);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"处理设备广播时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 异步获取设备的详细信息。
    /// </summary>
    /// <param name="deviceInfo">要填充的设备信息对象。</param>
    /// <param name="bluetoothAddress">设备的蓝牙地址。</param>
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

                // 如果已连接，则获取服务
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

    /// <summary>
    /// 处理观察者停止事件。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">事件参数。</param>
    private void OnWatcherStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        if (args.Error != BluetoothError.Success)
        {
            ErrorOccurred?.Invoke(this, $"扫描意外停止: {args.Error}");
            XTrace.WriteLine($"扫描意外停止: {args.Error}");
        }
    }

    /// <summary>
    /// 异步连接到指定的 BLE 设备。
    /// </summary>
    /// <param name="deviceInfo">要连接的设备的信息。</param>
    /// <returns>如果连接成功，则为 true；否则为 false。</returns>
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

    /// <summary>
    /// 断开与指定 BLE 设备的连接。
    /// </summary>
    /// <param name="deviceInfo">要断开连接的设备的信息。</param>
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

    /// <summary>
    /// 根据 UUID 获取已知的服务名称。
    /// </summary>
    /// <param name="uuid">服务的 UUID。</param>
    /// <returns>服务名称，如果未知则为空字符串。</returns>
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

    /// <summary>
    /// 根据 UUID 获取已知的特征名称。
    /// </summary>
    /// <param name="uuid">特征的 UUID。</param>
    /// <returns>特征名称，如果未知则为空字符串。</returns>
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

/// <summary>
/// 表示 BLE 设备的信息。
/// </summary>
public class BleDeviceInfo {
    /// <summary>
    /// 获取或设置设备 ID。
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// 获取或设置设备名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// 获取或设置设备的蓝牙地址。
    /// </summary>
    public ulong Address { get; set; }
    /// <summary>
    /// 获取或设置设备的接收信号强度指示 (RSSI)。
    /// </summary>
    public short Rssi { get; set; }
    /// <summary>
    /// 获取或设置上次看到设备的时间。
    /// </summary>
    public DateTime LastSeen { get; set; }
    /// <summary>
    /// 获取或设置一个值，该值指示设备是否可连接。
    /// </summary>
    public bool IsConnectable { get; set; }
    /// <summary>
    /// 获取或设置设备的服务数量。
    /// </summary>
    public int ServiceCount { get; set; }
    /// <summary>
    /// 获取或设置一个值，该值指示设备当前是否已连接。
    /// </summary>
    public bool IsConnected { get; set; }
    /// <summary>
    /// 获取或设置设备的服务列表。
    /// </summary>
    public List<BleServiceInfo> Services { get; set; } = new List<BleServiceInfo>();
    /// <summary>
    /// 获取或设置设备的广播数据。
    /// </summary>
    public List<BleAdvertisementData> AdvertisementData { get; set; } = new List<BleAdvertisementData>();
    /// <summary>
    /// 获取或设置设备的原始广播数据。
    /// </summary>
    public string RawAdvertisementData { get; set; } = string.Empty;
    /// <summary>
    /// 获取设备的连接状态。
    /// </summary>
    public string ConnectionStatus => IsConnected ? "已连接" : "未连接";

    /// <summary>
    /// 获取设备的显示名称。
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(Name) ? Id : $"{Name} ({Id})";
    /// <summary>
    /// 获取设备的信号强度。
    /// </summary>
    public string SignalStrength => $"{Rssi} dBm";
    /// <summary>
    /// 获取上次看到设备的时间。
    /// </summary>
    public string LastSeenTime => LastSeen.ToString("HH:mm:ss");
}

/// <summary>
/// 表示 BLE 服务的信息。
/// </summary>
public class BleServiceInfo {
    /// <summary>
    /// 获取或设置服务 ID。
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// 获取或设置服务的 UUID。
    /// </summary>
    public string Uuid { get; set; } = string.Empty;
    /// <summary>
    /// 获取或设置服务名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// 获取或设置服务的特征列表。
    /// </summary>
    public List<BleCharacteristicInfo> Characteristics { get; set; } = new List<BleCharacteristicInfo>();

    /// <summary>
    /// 获取服务的显示名称。
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(Name) ? Uuid : $"{Name} ({Uuid})";
}

/// <summary>
/// 表示 BLE 特征的信息。
/// </summary>
public class BleCharacteristicInfo {
    /// <summary>
    /// 获取或设置特征 ID。
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// 获取或设置特征的 UUID。
    /// </summary>
    public string Uuid { get; set; } = string.Empty;
    /// <summary>
    /// 获取或设置特征名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// 获取或设置一个值，该值指示特征是否可读。
    /// </summary>
    public bool CanRead { get; set; }
    /// <summary>
    /// 获取或设置一个值，该值指示特征是否可写。
    /// </summary>
    public bool CanWrite { get; set; }
    /// <summary>
    /// 获取或设置一个值，该值指示特征是否可以通知。
    /// </summary>
    public bool CanNotify { get; set; }
    /// <summary>
    /// 获取或设置特征的值。
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 获取特征的显示名称。
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(Name) ? Uuid : $"{Name} ({Uuid})";
    /// <summary>
    /// 获取特征的属性。
    /// </summary>
    public string Properties => $"{(CanRead ? "读 " : "")}{(CanWrite ? "写 " : "")}{(CanNotify ? "通知" : "")}";
}

/// <summary>
/// 表示 BLE 广播数据的信息。
/// </summary>
public class BleAdvertisementData {
    /// <summary>
    /// 获取或设置数据长度。
    /// </summary>
    public byte Length { get; set; }

    /// <summary>
    /// 获取或设置数据类型。
    /// </summary>
    public byte Type { get; set; }

    /// <summary>
    /// 获取或设置数据值。
    /// </summary>
    public byte[] Value { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// 获取数据类型的名称。
    /// </summary>
    public string TypeName => GetTypeName(Type);

    /// <summary>
    /// 获取数据值的十六进制字符串表示。
    /// </summary>
    public string ValueHex => BitConverter.ToString(Value).Replace("-", " ");

    /// <summary>
    /// 获取数据类型的名称。
    /// </summary>
    /// <param name="type">数据类型。</param>
    /// <returns>数据类型的名称。</returns>
    private static string GetTypeName(byte type) {
        return type switch {
            0x01 => "Flags",
            0x02 => "Incomplete List of 16-bit Service Class UUIDs",
            0x03 => "Complete List of 16-bit Service Class UUIDs",
            0x04 => "Incomplete List of 32-bit Service Class UUIDs",
            0x05 => "Complete List of 32-bit Service Class UUIDs",
            0x06 => "Incomplete List of 128-bit Service Class UUIDs",
            0x07 => "Complete List of 128-bit Service Class UUIDs",
            0x08 => "Shortened Local Name",
            0x09 => "Complete Local Name",
            0x0A => "TX Power Level",
            0x0D => "Class of Device",
            0x0E => "Simple Pairing Hash C",
            0x0F => "Simple Pairing Randomizer R",
            0x10 => "Device ID",
            0x16 => "Service Data - 16-bit UUID",
            0x20 => "Service Data - 32-bit UUID",
            0x21 => "Service Data - 128-bit UUID",
            0xFF => "Manufacturer Specific Data",
            _ => $"Unknown Type (0x{type:X2})"
        };
    }
}