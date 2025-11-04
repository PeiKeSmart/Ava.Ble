namespace JieLi.OTA.Core.Interfaces;

/// <summary>蓝牙设备接口</summary>
public interface IBluetoothDevice
{
    /// <summary>设备 ID</summary>
    string DeviceId { get; }

    /// <summary>设备名称</summary>
    string DeviceName { get; }

    /// <summary>信号强度 (dBm)</summary>
    short Rssi { get; }

    /// <summary>连接到设备</summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>断开连接</summary>
    Task DisconnectAsync();

    /// <summary>写入数据</summary>
    /// <param name="data">数据</param>
    Task<bool> WriteAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>订阅数据通知</summary>
    /// <param name="onDataReceived">数据接收回调</param>
    Task<bool> SubscribeNotifyAsync(Action<byte[]> onDataReceived, CancellationToken cancellationToken = default);

    /// <summary>数据接收事件</summary>
    event EventHandler<byte[]>? DataReceived;

    /// <summary>连接状态变更事件</summary>
    event EventHandler<bool>? ConnectionStatusChanged;
}
