using NewLife.Log;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;

namespace JieLi.OTA.Infrastructure.Bluetooth;

/// <summary>Windows BLE 服务</summary>
public class WindowsBleService : IDisposable
{
    private readonly BluetoothLEAdvertisementWatcher _watcher;
    private readonly Dictionary<ulong, BleDevice> _discoveredDevices = new();
    private readonly object _lockObj = new();
    private bool _disposed;

    /// <summary>是否正在扫描</summary>
    public bool IsScanning => _watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started;

    /// <summary>设备发现事件</summary>
    public event EventHandler<BleDevice>? DeviceDiscovered;

    /// <summary>设备更新事件</summary>
    public event EventHandler<BleDevice>? DeviceUpdated;

    public WindowsBleService()
    {
        _watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };

        _watcher.Received += OnAdvertisementReceived;
        _watcher.Stopped += OnWatcherStopped;
    }

    /// <summary>开始扫描</summary>
    public void StartScan()
    {
        if (IsScanning)
        {
            XTrace.WriteLine("[WindowsBleService] 已在扫描中");
            return;
        }

        lock (_lockObj)
        {
            _discoveredDevices.Clear();
        }

        _watcher.Start();
        XTrace.WriteLine("[WindowsBleService] 开始扫描设备");
    }

    /// <summary>停止扫描</summary>
    public void StopScan()
    {
        if (!IsScanning)
            return;

        _watcher.Stop();
        XTrace.WriteLine("[WindowsBleService] 停止扫描");
    }

    /// <summary>获取已发现的设备列表</summary>
    public List<BleDevice> GetDiscoveredDevices()
    {
        lock (_lockObj)
        {
            return _discoveredDevices.Values.OrderByDescending(d => d.Rssi).ToList();
        }
    }

    /// <summary>根据设备 ID 获取设备</summary>
    public BleDevice? GetDevice(string deviceId)
    {
        lock (_lockObj)
        {
            return _discoveredDevices.Values.FirstOrDefault(d => d.DeviceId == deviceId);
        }
    }

    /// <summary>连接到设备并订阅通知</summary>
    public async Task<BleDevice?> ConnectAndSubscribeAsync(
        string deviceId, 
        Action<byte[]> onDataReceived,
        CancellationToken cancellationToken = default)
    {
        var device = GetDevice(deviceId);
        if (device == null)
        {
            XTrace.WriteLine($"[WindowsBleService] 设备未找到: {deviceId}");
            return null;
        }

        // 连接设备
        bool connected = await device.ConnectAsync(cancellationToken);
        if (!connected)
        {
            XTrace.WriteLine($"[WindowsBleService] 连接失败: {device.DeviceName}");
            return null;
        }

        // 订阅通知
        bool subscribed = await device.SubscribeNotifyAsync(onDataReceived, cancellationToken);
        if (!subscribed)
        {
            XTrace.WriteLine($"[WindowsBleService] 订阅通知失败: {device.DeviceName}");
            await device.DisconnectAsync();
            return null;
        }

        XTrace.WriteLine($"[WindowsBleService] 连接并订阅成功: {device.DeviceName}");
        return device;
    }

    /// <summary>协商 MTU（最大传输单元）</summary>
    public async Task<int> NegotiateMtuAsync(BleDevice device, int requestedMtu = 512)
    {
        try
        {
            // Windows 通过 GattSession 协商 MTU
            var session = await Windows.Devices.Bluetooth.GenericAttributeProfile.GattSession
                .FromDeviceIdAsync(Windows.Devices.Bluetooth.BluetoothDeviceId.FromId(device.DeviceId));

            if (session == null)
            {
                XTrace.WriteLine("[WindowsBleService] 无法创建 GattSession");
                return 23; // 默认 ATT MTU
            }

            session.MaintainConnection = true;

            // MaxPduSize 属性直接返回协商后的 MTU
            int mtu = session.MaxPduSize;
            XTrace.WriteLine($"[WindowsBleService] MTU 协商结果: {mtu} bytes");

            return mtu;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return 23; // 返回默认值
        }
    }

    private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        try
        {
            var address = args.BluetoothAddress;
            var rssi = args.RawSignalStrengthInDBm;

            // 提取设备名称
            string deviceName = args.Advertisement.LocalName;
            if (string.IsNullOrEmpty(deviceName))
            {
                // 尝试从 Complete Local Name 数据段获取
                var nameSection = args.Advertisement.DataSections
                    .FirstOrDefault(s => s.DataType == 0x09); // Complete Local Name

                if (nameSection != null)
                {
                    var reader = Windows.Storage.Streams.DataReader.FromBuffer(nameSection.Data);
                    deviceName = reader.ReadString(reader.UnconsumedBufferLength);
                }
            }

            if (string.IsNullOrEmpty(deviceName))
            {
                // 尝试通过制造商数据或服务 UUID 推断设备类型
                string? deviceType = InferDeviceType(args.Advertisement);
                deviceName = string.IsNullOrEmpty(deviceType) 
                    ? $"Unknown_{address:X12}" 
                    : $"{deviceType}_{address:X12}";
            }

            // 生成设备 ID
            string deviceId = $"BluetoothLE#{address:X12}";

            lock (_lockObj)
            {
                if (_discoveredDevices.TryGetValue(address, out var existingDevice))
                {
                    // 更新现有设备
                    existingDevice.UpdateInfo(deviceName, rssi);
                    DeviceUpdated?.Invoke(this, existingDevice);
                }
                else
                {
                    // 添加新设备
                    var newDevice = new BleDevice(deviceId, deviceName, rssi, address);
                    _discoveredDevices[address] = newDevice;

                    XTrace.WriteLine($"[WindowsBleService] 发现设备: {deviceName} (RSSI: {rssi} dBm)");
                    DeviceDiscovered?.Invoke(this, newDevice);
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    private void OnWatcherStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        XTrace.WriteLine($"[WindowsBleService] 扫描已停止: {args.Error}");
    }

    /// <summary>根据广播数据推断设备类型</summary>
    private string? InferDeviceType(BluetoothLEAdvertisement advertisement)
    {
        // 检查制造商数据
        var manufacturerData = advertisement.ManufacturerData.FirstOrDefault();
        if (manufacturerData != null)
        {
            return manufacturerData.CompanyId switch
            {
                0x004C => "Apple",      // Apple Inc.
                0x0006 => "Microsoft",  // Microsoft
                0x00E0 => "Google",     // Google
                0x0075 => "Samsung",    // Samsung Electronics Co. Ltd.
                0x000F => "Broadcom",   // Broadcom
                0x0059 => "Nordic",     // Nordic Semiconductor ASA
                0x02E5 => "JL",         // 杰理科技 (假设的公司ID,需要确认实际值)
                _ => null
            };
        }

        // 检查服务 UUID
        if (advertisement.ServiceUuids.Count > 0)
        {
            var serviceUuid = advertisement.ServiceUuids[0].ToString().ToUpper();
            
            // 常见 BLE 服务类型
            if (serviceUuid.StartsWith("0000180F")) return "Battery";
            if (serviceUuid.StartsWith("0000180A")) return "DeviceInfo";
            if (serviceUuid.StartsWith("0000180D")) return "HeartRate";
            if (serviceUuid.StartsWith("00001812")) return "HID";
            if (serviceUuid.StartsWith("0000FE9F")) return "Google";
            if (serviceUuid.StartsWith("0000FEAA")) return "Google-Eddystone";
            if (serviceUuid.StartsWith("0000FD6F")) return "Apple";
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        StopScan();

        _watcher.Received -= OnAdvertisementReceived;
        _watcher.Stopped -= OnWatcherStopped;

        lock (_lockObj)
        {
            foreach (var device in _discoveredDevices.Values)
            {
                device.Dispose();
            }
            _discoveredDevices.Clear();
        }

        XTrace.WriteLine("[WindowsBleService] 已释放");
    }
}
