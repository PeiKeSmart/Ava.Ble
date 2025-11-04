using JieLi.OTA.Core.Interfaces;
using NewLife.Log;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace JieLi.OTA.Infrastructure.Bluetooth;

/// <summary>蓝牙设备封装</summary>
public class BleDevice : IBluetoothDevice, IDisposable
{
    private BluetoothLEDevice? _device;
    private GattCharacteristic? _writeCharacteristic;
    private GattCharacteristic? _notifyCharacteristic;
    private bool _disposed;

    /// <summary>设备 ID</summary>
    public string DeviceId { get; }

    /// <summary>设备名称</summary>
    public string DeviceName { get; private set; }

    /// <summary>信号强度 (dBm)</summary>
    public short Rssi { get; private set; }

    /// <summary>蓝牙地址</summary>
    public ulong BluetoothAddress { get; }

    /// <summary>是否已连接</summary>
    public bool IsConnected => _device?.ConnectionStatus == BluetoothConnectionStatus.Connected;

    /// <summary>数据接收事件</summary>
    public event EventHandler<byte[]>? DataReceived;

    /// <summary>连接状态变更事件</summary>
    public event EventHandler<bool>? ConnectionStatusChanged;

    public BleDevice(string deviceId, string deviceName, short rssi, ulong bluetoothAddress)
    {
        DeviceId = deviceId;
        DeviceName = deviceName;
        Rssi = rssi;
        BluetoothAddress = bluetoothAddress;
    }

    /// <summary>更新设备信息</summary>
    public void UpdateInfo(string deviceName, short rssi)
    {
        DeviceName = deviceName;
        Rssi = rssi;
    }

    /// <summary>连接到设备</summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 从 BluetoothAddress 创建设备
            _device = await BluetoothLEDevice.FromBluetoothAddressAsync(BluetoothAddress);
            if (_device == null)
            {
                XTrace.WriteLine($"[BleDevice] 无法创建设备: {DeviceId}");
                return false;
            }

            // 监听连接状态变化
            _device.ConnectionStatusChanged += OnConnectionStatusChanged;

            // 获取 GATT 服务
            var servicesResult = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
            if (servicesResult.Status != GattCommunicationStatus.Success)
            {
                XTrace.WriteLine($"[BleDevice] 获取服务失败: {servicesResult.Status}");
                return false;
            }

            // 查找 RCSP 服务和特征值
            bool found = await FindRcspCharacteristicsAsync(servicesResult.Services);
            if (!found)
            {
                XTrace.WriteLine($"[BleDevice] 未找到 RCSP 服务或特征值");
                return false;
            }

            XTrace.WriteLine($"[BleDevice] 连接成功: {DeviceName}");
            return true;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return false;
        }
    }

    /// <summary>断开连接</summary>
    public Task DisconnectAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    /// <summary>写入数据</summary>
    public async Task<bool> WriteAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (_writeCharacteristic == null)
        {
            XTrace.WriteLine("[BleDevice] 写特征值未初始化");
            return false;
        }

        try
        {
            var writer = new DataWriter();
            writer.WriteBytes(data);
            var buffer = writer.DetachBuffer();

            var result = await _writeCharacteristic.WriteValueWithResultAsync(
                buffer,
                GattWriteOption.WriteWithoutResponse);

            if (result.Status == GattCommunicationStatus.Success)
            {
                XTrace.WriteLine($"[BleDevice] 写入成功: {data.Length} bytes");
                return true;
            }

            XTrace.WriteLine($"[BleDevice] 写入失败: {result.Status}");
            return false;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return false;
        }
    }

    /// <summary>订阅数据通知</summary>
    public async Task<bool> SubscribeNotifyAsync(Action<byte[]> onDataReceived, CancellationToken cancellationToken = default)
    {
        if (_notifyCharacteristic == null)
        {
            XTrace.WriteLine("[BleDevice] 通知特征值未初始化");
            return false;
        }

        try
        {
            // 配置 CCCD（Client Characteristic Configuration Descriptor）
            var status = await _notifyCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);

            if (status != GattCommunicationStatus.Success)
            {
                XTrace.WriteLine($"[BleDevice] 订阅通知失败: {status}");
                return false;
            }

            // 注册数据接收事件
            _notifyCharacteristic.ValueChanged += OnCharacteristicValueChanged;

            XTrace.WriteLine("[BleDevice] 订阅通知成功");
            return true;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return false;
        }
    }

    private async Task<bool> FindRcspCharacteristicsAsync(IReadOnlyList<GattDeviceService> services)
    {
        // 遍历所有服务查找 Write 和 Notify 特征值
        foreach (var service in services)
        {
            var charsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
            if (charsResult.Status != GattCommunicationStatus.Success)
                continue;

            foreach (var characteristic in charsResult.Characteristics)
            {
                var properties = characteristic.CharacteristicProperties;

                // 查找写特征值
                if (_writeCharacteristic == null &&
                    (properties.HasFlag(GattCharacteristicProperties.Write) ||
                     properties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)))
                {
                    _writeCharacteristic = characteristic;
                    XTrace.WriteLine($"[BleDevice] 找到写特征值: {characteristic.Uuid}");
                }

                // 查找通知特征值
                if (_notifyCharacteristic == null &&
                    properties.HasFlag(GattCharacteristicProperties.Notify))
                {
                    _notifyCharacteristic = characteristic;
                    XTrace.WriteLine($"[BleDevice] 找到通知特征值: {characteristic.Uuid}");
                }

                // 如果都找到了就退出
                if (_writeCharacteristic != null && _notifyCharacteristic != null)
                    return true;
            }
        }

        return _writeCharacteristic != null && _notifyCharacteristic != null;
    }

    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        bool isConnected = sender.ConnectionStatus == BluetoothConnectionStatus.Connected;
        XTrace.WriteLine($"[BleDevice] 连接状态变更: {(isConnected ? "已连接" : "已断开")}");
        ConnectionStatusChanged?.Invoke(this, isConnected);
    }

    private void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        try
        {
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            var data = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(data);

            XTrace.WriteLine($"[BleDevice] 接收数据: {data.Length} bytes");
            DataReceived?.Invoke(this, data);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            if (_notifyCharacteristic != null)
            {
                _notifyCharacteristic.ValueChanged -= OnCharacteristicValueChanged;
                _notifyCharacteristic = null;
            }

            if (_device != null)
            {
                _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
                _device.Dispose();
                _device = null;
            }

            _writeCharacteristic = null;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        XTrace.WriteLine($"[BleDevice] 已释放: {DeviceName}");
    }
}
