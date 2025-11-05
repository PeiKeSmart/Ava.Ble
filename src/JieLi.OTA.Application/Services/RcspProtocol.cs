using JieLi.OTA.Core.Interfaces;
using JieLi.OTA.Core.Protocols;
using JieLi.OTA.Core.Protocols.Commands;
using JieLi.OTA.Core.Protocols.Responses;
using JieLi.OTA.Infrastructure.Bluetooth;
using NewLife.Log;

namespace JieLi.OTA.Application.Services;

/// <summary>RCSP 协议实现</summary>
public class RcspProtocol : IRcspProtocol, IDisposable
{
    private readonly RcspDataHandler _dataHandler;
    private bool _disposed;
    private bool _isInitialized;

    public RcspProtocol(IBluetoothDevice device)
    {
        _dataHandler = new RcspDataHandler(device);
    }

    /// <summary>初始化协议（获取设备信息）</summary>
    public async Task<RspDeviceInfo> InitializeAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            XTrace.WriteLine("[RcspProtocol] 已初始化，跳过");
            throw new InvalidOperationException("协议已初始化");
        }

        try
        {
            // 订阅数据接收
            var subscribed = await _dataHandler.InitializeAsync(cancellationToken);
            if (!subscribed)
            {
                throw new InvalidOperationException("无法订阅设备数据通知");
            }

            XTrace.WriteLine("[RcspProtocol] 开始获取设备信息...");

            // 发送获取设备信息命令
            var command = new CmdGetTargetInfo();
            var response = await _dataHandler.SendCommandAsync<RspDeviceInfo>(command, 5000, cancellationToken);

            _isInitialized = true;
            XTrace.WriteLine($"[RcspProtocol] 设备信息获取成功: {response.DeviceName}, Version={response.VersionName} (Code={response.VersionCode})");

            return response;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            throw;
        }
    }

    /// <summary>发送命令并等待响应</summary>
    public async Task<TResponse> SendCommandAsync<TResponse>(RcspCommand command, int timeoutMs = 5000, CancellationToken cancellationToken = default)
        where TResponse : RcspResponse, new()
    {
        EnsureInitialized();

        return await _dataHandler.SendCommandAsync<TResponse>(command, timeoutMs, cancellationToken);
    }

    /// <summary>断开连接</summary>
    public async Task DisconnectAsync()
    {
        if (_dataHandler != null)
        {
            await Task.CompletedTask;
            XTrace.WriteLine("[RcspProtocol] 断开连接");
        }

        _isInitialized = false;
    }

    /// <summary>查询设备是否可更新</summary>
    public async Task<RspCanUpdate> InquireCanUpdateAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            XTrace.WriteLine("[RcspProtocol] 查询设备是否可更新...");

            var command = new CmdInquireCanUpdate();
            var response = await _dataHandler.SendCommandAsync<RspCanUpdate>(command, 5000, cancellationToken);

            XTrace.WriteLine($"[RcspProtocol] 设备更新查询结果: {response.Result}");

            return response;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            throw;
        }
    }

    /// <summary>读取文件偏移</summary>
    public async Task<RspFileOffset> ReadFileOffsetAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            XTrace.WriteLine("[RcspProtocol] 读取文件偏移...");

            var command = new CmdReadFileOffset();
            var response = await _dataHandler.SendCommandAsync<RspFileOffset>(command, 5000, cancellationToken);

            XTrace.WriteLine($"[RcspProtocol] 文件偏移: {response.Offset}");

            return response;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            throw;
        }
    }

    /// <summary>进入更新模式</summary>
    public async Task<bool> EnterUpdateModeAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            XTrace.WriteLine("[RcspProtocol] 进入更新模式...");

            var command = new CmdEnterUpdateMode();
            var response = await _dataHandler.SendCommandAsync<RspCanUpdate>(command, 5000, cancellationToken);

            var success = response.CanUpdate;
            XTrace.WriteLine($"[RcspProtocol] 进入更新模式: {(success ? "成功" : "失败")}");

            return success;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            throw;
        }
    }

    /// <summary>通知文件大小</summary>
    public async Task<bool> NotifyFileSizeAsync(uint fileSize, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            XTrace.WriteLine($"[RcspProtocol] 通知文件大小: {fileSize} bytes");

            var command = new CmdNotifyFileSize { TotalSize = fileSize };
            var response = await _dataHandler.SendCommandAsync<RspFileOffset>(command, 5000, cancellationToken);

            var success = response.Offset >= 0;
            XTrace.WriteLine($"[RcspProtocol] 通知文件大小: {(success ? "成功" : "失败")}");

            return success;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            throw;
        }
    }

    /// <summary>设备请求文件块事件</summary>
    public event EventHandler<RcspPacket>? DeviceRequestedFileBlock
    {
        add => _dataHandler.OnDeviceCommandReceived += value;
        remove => _dataHandler.OnDeviceCommandReceived -= value;
    }

    /// <summary>查询升级结果</summary>
    public async Task<RspUpdateResult> QueryUpdateResultAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            XTrace.WriteLine("[RcspProtocol] 查询升级结果...");
            var command = new CmdQueryUpdateResult();
            var response = await _dataHandler.SendCommandAsync<RspUpdateResult>(command, 5000, cancellationToken);
            XTrace.WriteLine($"[RcspProtocol] 升级结果: Status=0x{response.Status:X2}, Code=0x{response.ResultCode:X2}");
            return response;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            throw;
        }
    }

    /// <summary>获取缓存的设备命令</summary>
    public RcspPacket? GetCachedDeviceCommand(int offset, ushort length)
    {
        return _dataHandler.GetCachedDeviceCommand(offset, length);
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("协议未初始化，请先调用 InitializeAsync");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _dataHandler?.Dispose();
        _disposed = true;

        XTrace.WriteLine("[RcspProtocol] 已释放资源");
    }
}
