using NewLife.Log;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
            ScanningMode = BluetoothLEScanningMode.Active,
            SignalStrengthFilter = new BluetoothSignalStrengthFilter
            {
                SamplingInterval = TimeSpan.FromMilliseconds(100),
                // 不设置信号强度阈值，以接收所有设备
            }
        };

        // 不设置广播过滤器，以接收所有类型的广播数据

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

            // 用于存储当前接收到的所有解析后的广播数据段
            List<BleAdvertisementData> currentReceivedAdvertisementDataList = new List<BleAdvertisementData>();

            // 1. 处理原始广播数据 (DataSections)
            if (args.Advertisement.DataSections != null && args.Advertisement.DataSections.Count > 0)
            {
                foreach (var section in args.Advertisement.DataSections)
                {
                    var reader = Windows.Storage.Streams.DataReader.FromBuffer(section.Data);
                    byte[] data = new byte[section.Data.Length];
                    reader.ReadBytes(data);

                    currentReceivedAdvertisementDataList.Add(new BleAdvertisementData
                    {
                        Length = (byte)(data.Length + 1), // 数据长度 + 类型字段(1字节)
                        Type = section.DataType,
                        Value = data
                    });
                }
            }

            // 2. 处理制造商特定数据 (ManufacturerData)
            // 将CompanyID和数据合并到Value中，Length为(Value.Length + 1)
            if (args.Advertisement.ManufacturerData != null && args.Advertisement.ManufacturerData.Count > 0)
            {
                foreach (var manufacturerData in args.Advertisement.ManufacturerData)
                {
                    var actualDataBytes = new byte[manufacturerData.Data.Length];
                    Windows.Storage.Streams.DataReader.FromBuffer(manufacturerData.Data).ReadBytes(actualDataBytes);

                    byte[] companyIdBytes = BitConverter.GetBytes(manufacturerData.CompanyId); // 通常是 ushort (2 bytes)
                    // BLE Company ID 应该是小端序 (Little Endian)
                    // BitConverter.GetBytes 在小端系统上产生小端字节数组。

                    byte[] combinedValue = new byte[2 + actualDataBytes.Length];
                    Buffer.BlockCopy(companyIdBytes, 0, combinedValue, 0, 2);
                    Buffer.BlockCopy(actualDataBytes, 0, combinedValue, 2, actualDataBytes.Length);

                    currentReceivedAdvertisementDataList.Add(new BleAdvertisementData
                    {
                        Length = (byte)(combinedValue.Length + 1), // (CompanyID + Data).Length + Type_Field_Length
                        Type = 0xFF, // Manufacturer Specific Data
                        Value = combinedValue
                    });
                }
            }

            // 3. 服务UUID数据不再单独处理，它们应该包含在DataSections中对应的类型里。

            // --- 新增去重和排序逻辑 开始 ---
            var manufacturerDataInCurrentList = currentReceivedAdvertisementDataList
                .Where(ad => ad.Type == 0xFF)
                .ToList();
            var nonManufacturerDataInCurrentList = currentReceivedAdvertisementDataList
                .Where(ad => ad.Type != 0xFF)
                .ToList();

            var uniqueManufacturerData = manufacturerDataInCurrentList
                .GroupBy(ad => ad.ValueHex) // 使用 ValueHex (已包含 CompanyID 和 Data) 作为分组依据
                .Select(g => g.First())    // 每个组取第一个，实现去重
                .ToList();

            currentReceivedAdvertisementDataList = nonManufacturerDataInCurrentList;
            currentReceivedAdvertisementDataList.AddRange(uniqueManufacturerData);
            currentReceivedAdvertisementDataList = currentReceivedAdvertisementDataList
                                                    .OrderBy(ad => ad.Type)
                                                    .ThenBy(ad => ad.ValueHex) // 按类型，然后按数据内容排序
                                                    .ToList();
            // --- 新增去重和排序逻辑 结束 ---

            // 如果设备已存在，则合并更新信息
            if (_deviceCache.ContainsKey(deviceId))
            {
                var existingDevice = _deviceCache[deviceId];
                existingDevice.Rssi = args.RawSignalStrengthInDBm;
                existingDevice.LastSeen = DateTime.Now;

                // 1. 处理非制造商数据 (Types != 0xFF)
                var mergedNonManufacturerData = existingDevice.AdvertisementData
                                                    .Where(ad => ad.Type != 0xFF)
                                                    .ToDictionary(ad => ad.Type, ad => ad);

                foreach (var newAdData in currentReceivedAdvertisementDataList.Where(ad => ad.Type != 0xFF))
                {
                    mergedNonManufacturerData[newAdData.Type] = newAdData; // 更新或添加
                }

                // 2. 处理制造商数据 (Type == 0xFF) - 累积唯一值
                var existingManufacturerData = existingDevice.AdvertisementData
                                                    .Where(ad => ad.Type == 0xFF)
                                                    .ToList();
                var currentManufacturerDataFromAdv = currentReceivedAdvertisementDataList
                                                    .Where(ad => ad.Type == 0xFF)
                                                    .ToList(); // currentReceivedAdvertisementDataList中的0xFF数据已经针对当次广播去重

                // 合并已有的和当前新接收的制造商数据
                var combinedManufacturerData = existingManufacturerData;
                combinedManufacturerData.AddRange(currentManufacturerDataFromAdv);

                // 对合并后的所有制造商数据进行最终去重
                var uniqueTotalManufacturerData = combinedManufacturerData
                                                    .GroupBy(ad => ad.ValueHex) // 根据ValueHex去重，确保CompanyID+Data的唯一性
                                                    .Select(g => g.First())
                                                    .ToList();

                // 3. 组合新的 AdvertisementData 列表
                var newFullAdvertisementData = mergedNonManufacturerData.Values.ToList();
                newFullAdvertisementData.AddRange(uniqueTotalManufacturerData); // 添加累积且去重后的制造商数据

                existingDevice.AdvertisementData = newFullAdvertisementData
                                                    .OrderBy(ad => ad.Type)
                                                    .ThenBy(ad => ad.ValueHex)
                                                    .ToList();

                // 重新构建 RawAdvertisementData 字符串
                string newRawData = string.Empty;
                foreach (var adData in existingDevice.AdvertisementData)
                {
                    newRawData += $"{adData.Length:X2} {adData.Type:X2} {adData.ValueHex} ";
                }
                existingDevice.RawAdvertisementData = newRawData.Trim();

                // Attempt to parse version for HLK-LD2410 devices
                if (existingDevice.Name != null && existingDevice.Name.Contains("HLK-LD2410"))
                {
                    existingDevice.Version = TryParseHlkLd2410Version(existingDevice.AdvertisementData);
                }

                DeviceDiscovered?.Invoke(this, existingDevice);
                return;
            }

            // 如果是新设备，则创建新的设备信息
            // currentReceivedAdvertisementDataList 此时已经去重和排序过了
            string initialRawData = string.Empty;
            foreach (var adData in currentReceivedAdvertisementDataList) // currentReceivedAdvertisementDataList 已排序
            {
                initialRawData += $"{adData.Length:X2} {adData.Type:X2} {adData.ValueHex} ";
            }

            var deviceInfo = new BleDeviceInfo
            {
                Id = deviceId,
                Name = deviceName,
                Address = args.BluetoothAddress,
                Rssi = args.RawSignalStrengthInDBm,
                LastSeen = DateTime.Now,
                AdvertisementData = currentReceivedAdvertisementDataList, // 使用去重和排序后的列表
                RawAdvertisementData = initialRawData.Trim()
            };

            // Attempt to parse version for HLK-LD2410 devices
            if (deviceInfo.Name != null && deviceInfo.Name.Contains("HLK-LD2410"))
            {
                deviceInfo.Version = TryParseHlkLd2410Version(deviceInfo.AdvertisementData);
            }

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
    /// 尝试为 HLK-LD2410 设备解析版本号。
    /// </summary>
    /// <param name="advertisementDataList">设备的广播数据列表。</param>
    /// <returns>解析出的版本号字符串，如果无法解析则返回 null。</returns>
    private string? TryParseHlkLd2410Version(List<BleAdvertisementData> advertisementDataList)
    {
        foreach (var ad in advertisementDataList)
        {
            if (ad.Type == 0xFF) // Manufacturer Specific Data
            {
                if (ad.Value != null && ad.Value.Length == 15)
                {
                    // Extract the 6 bytes for version starting from the 3rd byte of Value
                    // (0-indexed: Value[2] to Value[7])
                    byte ext1 = ad.Value[2]; // Byte for B1 role (e.g., 09)
                    byte ext2 = ad.Value[3]; // Byte for B2 role (e.g., 02)
                    byte ext3 = ad.Value[4]; // Byte for B3 role (e.g., 17)
                    byte ext4 = ad.Value[5]; // Byte for B4 role (e.g., 09)
                    byte ext5 = ad.Value[6]; // Byte for B5 role (e.g., 05)
                    byte ext6 = ad.Value[7]; // Byte for B6 role (e.g., 25)

                    // Format: {B2_dec}.{B1_dec:D2}.{B6_hex:X2}{B5_hex:X2}{B4_hex:X2}{B3_hex:X2}
                    // Example: input 09 02 17 09 05 25 (as ext1 to ext6) -> 2.09.25050917
                    return $"{ext2}.{ext1:D2}.{ext6:X2}{ext5:X2}{ext4:X2}{ext3:X2}";
                }
            }
        }
        return null; // Version not found or data not matching criteria
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
public class BleDeviceInfo : INotifyPropertyChanged {
    private string _id = string.Empty;
    private string _name = string.Empty;
    private ulong _address;
    private short _rssi;
    private DateTime _lastSeen;
    private bool _isConnectable;
    private int _serviceCount;
    private bool _isConnected;
    private List<BleServiceInfo> _services = new List<BleServiceInfo>();
    private List<BleAdvertisementData> _advertisementData = new List<BleAdvertisementData>();
    private string _rawAdvertisementData = string.Empty;
    private string? _version; // Added Version property

    /// <summary>
    /// 获取或设置设备 ID。
    /// </summary>
    public string Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// 获取或设置设备名称。
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// 获取或设置设备的蓝牙地址。
    /// </summary>
    public ulong Address
    {
        get => _address;
        set
        {
            if (_address != value)
            {
                _address = value;
                OnPropertyChanged(nameof(Address));
            }
        }
    }

    /// <summary>
    /// 获取或设置设备的接收信号强度指示 (RSSI)。
    /// </summary>
    public short Rssi
    {
        get => _rssi;
        set
        {
            if (_rssi != value)
            {
                _rssi = value;
                OnPropertyChanged(nameof(Rssi));
                OnPropertyChanged(nameof(SignalStrength));
            }
        }
    }

    /// <summary>
    /// 获取或设置上次看到设备的时间。
    /// </summary>
    public DateTime LastSeen
    {
        get => _lastSeen;
        set
        {
            if (_lastSeen != value)
            {
                _lastSeen = value;
                OnPropertyChanged(nameof(LastSeen));
                OnPropertyChanged(nameof(LastSeenTime));
            }
        }
    }

    /// <summary>
    /// 获取或设置一个值，该值指示设备是否可连接。
    /// </summary>
    public bool IsConnectable
    {
        get => _isConnectable;
        set
        {
            if (_isConnectable != value)
            {
                _isConnectable = value;
                OnPropertyChanged(nameof(IsConnectable));
            }
        }
    }

    /// <summary>
    /// 获取或设置设备的服务数量。
    /// </summary>
    public int ServiceCount
    {
        get => _serviceCount;
        set
        {
            if (_serviceCount != value)
            {
                _serviceCount = value;
                OnPropertyChanged(nameof(ServiceCount));
            }
        }
    }

    /// <summary>
    /// 获取或设置一个值，该值指示设备当前是否已连接。
    /// </summary>
    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (_isConnected != value)
            {
                _isConnected = value;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectionStatus));
            }
        }
    }

    /// <summary>
    /// 获取或设置设备的服务列表。
    /// </summary>
    public List<BleServiceInfo> Services
    {
        get => _services;
        set
        {
            _services = value;
            OnPropertyChanged(nameof(Services));
        }
    }

    /// <summary>
    /// 获取或设置设备的广播数据。
    /// </summary>
    public List<BleAdvertisementData> AdvertisementData
    {
        get => _advertisementData;
        set
        {
            _advertisementData = value;
            OnPropertyChanged(nameof(AdvertisementData));
        }
    }

    /// <summary>
    /// 获取或设置设备的原始广播数据。
    /// </summary>
    public string RawAdvertisementData
    {
        get => _rawAdvertisementData;
        set
        {
            if (_rawAdvertisementData != value)
            {
                _rawAdvertisementData = value;
                OnPropertyChanged(nameof(RawAdvertisementData));
            }
        }
    }

    /// <summary>
    /// 获取或设置设备解析出的版本号。
    /// </summary>
    public string? Version
    {
        get => _version;
        set
        {
            if (_version != value)
            {
                _version = value;
                OnPropertyChanged(nameof(Version));
            }
        }
    }

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

    /// <summary>
    /// 在属性值更改时发生。
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 引发 PropertyChanged 事件。
    /// </summary>
    /// <param name="propertyName">已更改的属性的名称。</param>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
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
    /// 获取或设置数据长度（包括类型字段的长度）。
    /// 在蓝牙广播数据格式中，长度字段表示类型字段和数据值的总长度。
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