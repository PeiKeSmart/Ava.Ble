using System.Diagnostics;
using JieLi.OTA.Core.Interfaces;
using JieLi.OTA.Core.Models;
using JieLi.OTA.Core.Protocols;
using JieLi.OTA.Core.Protocols.Responses;
using JieLi.OTA.Infrastructure.Bluetooth;
using JieLi.OTA.Infrastructure.FileSystem;
using NewLife.Log;

namespace JieLi.OTA.Application.Services;

/// <summary>OTA 管理器实现</summary>
public class OtaManager : IOtaManager
{
    private readonly WindowsBleService _bleService;
    private readonly OtaFileService _fileService;
    private readonly ReconnectService _reconnectService;
    
    private BleDevice? _currentDevice;
    private RcspProtocol? _protocol;
    private byte[]? _firmwareData;
    private int _sentBytes;
    private readonly Stopwatch _speedWatch = new();
    private bool _disposed;

    private OtaState _currentState = OtaState.Idle;
    private OtaProgress _progress = new();
    private readonly Stopwatch _totalTimeWatch = new();
    private RspDeviceInfo? _deviceInfo;
    
    public OtaConfig Config { get; set; } = new();
    
    public event EventHandler<OtaState>? StateChanged;
    public event EventHandler<OtaProgress>? ProgressChanged;
    
    private event Action<int, string>? ErrorOccurred;

    public OtaManager(WindowsBleService bleService, OtaFileService fileService)
    {
        _bleService = bleService;
        _fileService = fileService;
        _reconnectService = new ReconnectService(bleService);
    }

    /// <summary>启动 OTA 升级</summary>
    public async Task<OtaResult> StartOtaAsync(string deviceId, string firmwareFilePath, CancellationToken cancellationToken = default)
    {
        if (_currentState != OtaState.Idle)
        {
            return new OtaResult
            {
                Success = false,
                ErrorCode = -1,
                ErrorMessage = "OTA 升级已在进行中",
                FinalState = _currentState
            };
        }

        _totalTimeWatch.Restart();

        try
        {
            // 1. 验证固件文件
            ChangeState(OtaState.ValidatingFirmware);
            var (isValid, message, fileData) = _fileService.ValidateFile(firmwareFilePath);
            if (!isValid || fileData == null)
            {
                return CreateErrorResult(-1, message);
            }

            _firmwareData = fileData;
            _sentBytes = 0;
            _progress = new OtaProgress
            {
                TotalBytes = fileData.Length,
                TransferredBytes = 0,
                Speed = 0,
                State = OtaState.ValidatingFirmware
            };

            XTrace.WriteLine($"[OtaManager] 固件文件验证成功: {fileData.Length} bytes");

            // 2. 连接设备
            ChangeState(OtaState.Connecting);
            _currentDevice = _bleService.GetDiscoveredDevices()
                .FirstOrDefault(d => d.DeviceId == deviceId);

            if (_currentDevice == null)
            {
                return CreateErrorResult(-1, "未找到指定设备");
            }

            var connected = await _currentDevice.ConnectAsync(cancellationToken);
            if (!connected)
            {
                return CreateErrorResult(OtaErrorCode.ERROR_CONNECTION_LOST, "连接设备失败");
            }

            XTrace.WriteLine($"[OtaManager] 设备连接成功: {_currentDevice.DeviceName}");

            // 3. 初始化协议（获取设备信息）
            ChangeState(OtaState.GettingDeviceInfo);
            _protocol = new RcspProtocol(_currentDevice);

            // 订阅设备请求文件块事件
            _protocol.DeviceRequestedFileBlock += OnDeviceRequestedFileBlock;

            _deviceInfo = await _protocol.InitializeAsync(deviceId, cancellationToken);
            XTrace.WriteLine($"[OtaManager] 设备信息: {_deviceInfo.DeviceName}, Version={_deviceInfo.VersionName}, Battery={_deviceInfo.BatteryLevel}%");

            // 4. 查询是否可更新
            ChangeState(OtaState.GettingDeviceInfo);
            var canUpdate = await _protocol.InquireCanUpdateAsync(cancellationToken);
            if (!canUpdate.CanUpdate)
            {
                return CreateErrorResult(-1, $"设备不支持更新: {canUpdate}");
            }

            XTrace.WriteLine("[OtaManager] 设备支持更新");

            // 5. 读取文件偏移（断点续传）
            ChangeState(OtaState.ReadingFileOffset);
            var fileOffset = await _protocol.ReadFileOffsetAsync(cancellationToken);
            _sentBytes = (int)fileOffset.Offset;

            if (_sentBytes > 0)
            {
                XTrace.WriteLine($"[OtaManager] 检测到断点续传，从偏移 {_sentBytes} 开始");
            }

            // 6. 进入更新模式
            ChangeState(OtaState.EnteringUpdateMode);
            var enterSuccess = await _protocol.EnterUpdateModeAsync(cancellationToken);
            if (!enterSuccess)
            {
                return CreateErrorResult(OtaErrorCode.ERROR_OTA_FAIL, "进入更新模式失败");
            }

            XTrace.WriteLine("[OtaManager] 已进入更新模式");

            // 7. 通知文件大小
            ChangeState(OtaState.EnteringUpdateMode);
            var notifySuccess = await _protocol.NotifyFileSizeAsync((uint)fileData.Length, cancellationToken);
            if (!notifySuccess)
            {
                return CreateErrorResult(OtaErrorCode.ERROR_OTA_FAIL, "通知文件大小失败");
            }

            XTrace.WriteLine($"[OtaManager] 已通知文件大小: {fileData.Length} bytes");

            // 8. 传输固件数据
            ChangeState(OtaState.TransferringFile);
            _speedWatch.Restart();

            // 等待设备请求文件块（通过事件处理）
            XTrace.WriteLine("[OtaManager] 等待设备请求文件块...");

            // 等待传输完成或超时
            var transferTimeout = TimeSpan.FromMinutes(10); // 默认10分钟
            var transferTask = WaitForTransferCompleteAsync(cancellationToken);
            var completedTask = await Task.WhenAny(transferTask, Task.Delay(transferTimeout, cancellationToken));

            if (completedTask != transferTask)
            {
                return CreateErrorResult(OtaErrorCode.ERROR_COMMAND_TIMEOUT, "固件传输超时");
            }

            var transferSuccess = await transferTask;
            if (!transferSuccess)
            {
                return CreateErrorResult(OtaErrorCode.ERROR_OTA_FAIL, "固件传输失败");
            }

            _speedWatch.Stop();
            XTrace.WriteLine("[OtaManager] 固件传输完成");

            // 9. 等待设备重连（设备重启应用固件）
            if (true) // 总是等待重连
            {
                ChangeState(OtaState.WaitingReconnect);
                XTrace.WriteLine("[OtaManager] 等待设备重连...");

                var reconnectedDevice = await _reconnectService.WaitForReconnectAsync(
                    _currentDevice.BluetoothAddress,
                    useNewMacMethod: true,
                    timeoutMs: Config.ReconnectTimeout,
                    cancellationToken: cancellationToken);

                if (reconnectedDevice == null)
                {
                    return CreateErrorResult(OtaErrorCode.ERROR_RECONNECT_TIMEOUT, "设备重连超时");
                }

                if (reconnectedDevice != null)
                {
                    XTrace.WriteLine($"[OtaManager] 设备重连成功: {reconnectedDevice.DeviceName}");
                }
            }

            // 10. 完成
            ChangeState(OtaState.Completed);
            _totalTimeWatch.Stop();
            XTrace.WriteLine("[OtaManager] OTA 升级成功完成！");

            return new OtaResult
            {
                Success = true,
                ErrorCode = OtaErrorCode.SUCCESS,
                ErrorMessage = "升级成功",
                DeviceInfo = _deviceInfo,
                FinalState = OtaState.Completed,
                TotalTime = _totalTimeWatch.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            ChangeState(OtaState.Failed);
            return CreateErrorResult(OtaErrorCode.ERROR_USER_CANCELLED, "OTA 升级已取消");
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            ChangeState(OtaState.Failed);
            return CreateErrorResult(OtaErrorCode.ERROR_OTA_FAIL, $"OTA 升级异常: {ex.Message}");
        }
        finally
        {
            CleanupResources();
        }
    }

    /// <summary>创建错误结果</summary>
    private OtaResult CreateErrorResult(int errorCode, string message)
    {
        _totalTimeWatch.Stop();
        ErrorOccurred?.Invoke(errorCode, message);

        return new OtaResult
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = message,
            DeviceInfo = _deviceInfo,
            FinalState = _currentState,
            TotalTime = _totalTimeWatch.Elapsed
        };
    }

    /// <summary>取消 OTA 升级</summary>
    public Task CancelOtaAsync()
    {
        if (_currentState == OtaState.Idle || _currentState == OtaState.Completed || _currentState == OtaState.Failed)
        {
            return Task.CompletedTask;
        }

        XTrace.WriteLine("[OtaManager] 取消 OTA 升级");
        ChangeState(OtaState.Failed);
        CleanupResources();

        return Task.CompletedTask;
    }

    /// <summary>处理设备请求文件块事件</summary>
    private async void OnDeviceRequestedFileBlock(object? sender, RcspPacket packet)
    {
        if (_firmwareData == null || _currentDevice == null || _currentState != OtaState.TransferringFile)
        {
            return;
        }

        try
        {
            // 解析请求：Sn (1 byte) + offset (4 bytes) + length (2 bytes)
            if (packet.Payload.Length < 7)
            {
                XTrace.WriteLine("[OtaManager] 无效的文件块请求");
                return;
            }

            var offset = BitConverter.ToInt32(packet.Payload, 1); // 从索引1开始读取offset
            var length = BitConverter.ToUInt16(packet.Payload, 5); // 从索引5开始读取length

            // 从缓存中获取原始命令包（包含正确的 Sn）
            var cachedCommand = _protocol.GetCachedDeviceCommand(offset, length) ?? packet;
            if (cachedCommand == packet)
            {
                XTrace.WriteLine($"[OtaManager] 警告: 未找到缓存的命令 offset={offset}, len={length}，使用当前packet");
            }
            
            var sn = cachedCommand.Payload[0]; // 从缓存的命令中获取正确的 Sn

            // 读取文件块
            var block = _fileService.ReadFileBlock(_firmwareData, offset, length);

            // 构造响应：Status (1) + Sn (1) + offset (4) + length (2) + block data
            var responsePayload = new byte[1 + 1 + 4 + 2 + block.Length];
            responsePayload[0] = 0x00; // Status: 成功
            responsePayload[1] = sn;   // 使用请求中的 Sn
            BitConverter.GetBytes(offset).CopyTo(responsePayload, 2);
            BitConverter.GetBytes(length).CopyTo(responsePayload, 6);
            block.CopyTo(responsePayload, 8);

            // 创建响应包
            var responsePacket = new RcspPacket
            {
                Flag = 0x00, // 响应包
                OpCode = OtaOpCode.CMD_OTA_FILE_BLOCK,
                Payload = responsePayload
            };

            // 发送响应
            await _currentDevice.WriteAsync(responsePacket.ToBytes());

            // 更新进度
            _sentBytes = offset + block.Length;
            UpdateProgress();

            XTrace.WriteLine($"[OtaManager] 发送文件块: offset={offset}, length={block.Length}, 进度={_progress.Percentage}%");
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            ErrorOccurred?.Invoke(OtaErrorCode.ERROR_OTA_FAIL, $"发送文件块失败: {ex.Message}");
        }
    }

    /// <summary>等待传输完成</summary>
    private async Task<bool> WaitForTransferCompleteAsync(CancellationToken cancellationToken)
    {
        while (_sentBytes < (_firmwareData?.Length ?? 0) && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
        }

        return _sentBytes >= (_firmwareData?.Length ?? 0);
    }

    /// <summary>更新进度</summary>
    private void UpdateProgress()
    {
        if (_firmwareData == null) return;

        var elapsedSeconds = _speedWatch.Elapsed.TotalSeconds;
        var speed = elapsedSeconds > 0 ? (long)(_sentBytes / elapsedSeconds) : 0;

        _progress = new OtaProgress
        {
            TotalBytes = _firmwareData.Length,
            TransferredBytes = _sentBytes,
            Speed = speed,
            State = _currentState
        };

        ProgressChanged?.Invoke(this, _progress);
    }

    /// <summary>改变状态</summary>
    private void ChangeState(OtaState newState)
    {
        if (_currentState == newState) return;

        _currentState = newState;
        
        // 更新进度状态
        _progress.State = newState;

        StateChanged?.Invoke(this, newState);
        XTrace.WriteLine($"[OtaManager] 状态变更: {newState}");
    }

    /// <summary>清理资源</summary>
    private void CleanupResources()
    {
        if (_protocol != null)
        {
            _protocol.DeviceRequestedFileBlock -= OnDeviceRequestedFileBlock;
            _protocol.Dispose();
            _protocol = null;
        }

        _currentDevice = null;
        _firmwareData = null;
        _sentBytes = 0;
        _speedWatch.Reset();
    }

    public void Dispose()
    {
        if (_disposed) return;

        CleanupResources();
        _disposed = true;

        XTrace.WriteLine("[OtaManager] 已释放资源");
    }
}
