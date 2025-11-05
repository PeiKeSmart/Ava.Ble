using JieLi.OTA.Infrastructure.Bluetooth;
using NewLife.Log;

namespace JieLi.OTA.Application.Services;

/// <summary>断线重连服务</summary>
public class ReconnectService
{
    private readonly WindowsBleService _bleService;
    private readonly int _defaultTimeoutMs = 30000; // 默认 30 秒超时

    public ReconnectService(WindowsBleService bleService)
    {
        _bleService = bleService;
    }

    /// <summary>等待设备重连</summary>
    /// <param name="originalAddress">原始设备地址</param>
    /// <param name="useNewMacMethod">是否使用新 MAC 地址匹配方法</param>
    /// <param name="timeoutMs">超时时间（毫秒）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重连的设备，超时返回 null</returns>
    public async Task<BleDevice?> WaitForReconnectAsync(
        ulong originalAddress,
        bool useNewMacMethod = true,
        int timeoutMs = 0,
        CancellationToken cancellationToken = default)
    {
        if (timeoutMs <= 0)
        {
            timeoutMs = _defaultTimeoutMs;
        }

        XTrace.WriteLine($"[ReconnectService] 等待设备重连，原地址: 0x{originalAddress:X12}, 超时: {timeoutMs}ms");

        var tcs = new TaskCompletionSource<BleDevice?>();
        BleDevice? reconnectedDevice = null;

        // 订阅设备发现事件
        void OnDeviceDiscovered(object? sender, BleDevice device)
        {
            try
            {
                var isMatch = useNewMacMethod
                    ? IsNewMacMatch(originalAddress, device.BluetoothAddress)
                    : IsOldMacMatch(originalAddress, device.BluetoothAddress);

                if (isMatch)
                {
                    XTrace.WriteLine($"[ReconnectService] 发现匹配设备: {device.DeviceName}, 地址: 0x{device.BluetoothAddress:X12}");
                    reconnectedDevice = device;
                    // 连接设备
                    _ = ConnectDeviceAsync(device, tcs, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }

        _bleService.DeviceDiscovered += OnDeviceDiscovered;

        try
        {
            // 开始扫描
            _bleService.StartScan();

            // 等待设备重连或超时
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            var delayTask = Task.Delay(timeoutMs, cts.Token);
            var reconnectTask = tcs.Task;

            var completedTask = await Task.WhenAny(reconnectTask, delayTask);

            if (completedTask == reconnectTask)
            {
                XTrace.WriteLine("[ReconnectService] 设备重连成功");
                return reconnectedDevice;
            }

            XTrace.WriteLine("[ReconnectService] 等待设备重连超时");
            return null;
        }
        catch (OperationCanceledException)
        {
            XTrace.WriteLine("[ReconnectService] 等待设备重连已取消");
            return null;
        }
        finally
        {
            _bleService.DeviceDiscovered -= OnDeviceDiscovered;
            _bleService.StopScan();
        }
    }

    /// <summary>新 MAC 地址匹配方法（单备份）</summary>
    /// <remarks>地址低 3 字节 +1</remarks>
    private bool IsNewMacMatch(ulong originalAddress, ulong newAddress)
    {
        // 提取低 3 字节
        var originalLow3 = originalAddress & 0xFFFFFF;
        var newLow3 = newAddress & 0xFFFFFF;

        // 提取高 3 字节
        var originalHigh3 = (originalAddress >> 24) & 0xFFFFFF;
        var newHigh3 = (newAddress >> 24) & 0xFFFFFF;

        // 低 3 字节 +1，高 3 字节不变
        return (originalLow3 + 1) == newLow3 && originalHigh3 == newHigh3;
    }

    /// <summary>旧 MAC 地址匹配方法（双备份）</summary>
    /// <remarks>地址最低字节 +2</remarks>
    private bool IsOldMacMatch(ulong originalAddress, ulong newAddress)
    {
        // 提取最低字节
        var originalLowest = originalAddress & 0xFF;
        var newLowest = newAddress & 0xFF;

        // 提取其余字节
        var originalRest = originalAddress & 0xFFFFFFFFFFFF00;
        var newRest = newAddress & 0xFFFFFFFFFFFF00;

        // 最低字节 +2，其余字节不变
        return (originalLowest + 2) == newLowest && originalRest == newRest;
    }

    /// <summary>连接设备</summary>
    private async Task ConnectDeviceAsync(BleDevice device, TaskCompletionSource<BleDevice?> tcs, CancellationToken cancellationToken)
    {
        try
        {
            var connected = await device.ConnectAsync(cancellationToken);
            if (connected)
            {
                XTrace.WriteLine($"[ReconnectService] 设备连接成功: {device.DeviceName}");
                tcs.TrySetResult(device);
            }
            else
            {
                XTrace.WriteLine($"[ReconnectService] 设备连接失败: {device.DeviceName}");
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine($"[ReconnectService] 设备连接异常: {device.DeviceName}, {ex.Message}");
        }
    }
}
