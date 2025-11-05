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
    private IReadyToReconnectStrategy _readyStrategy;
    
    private IBluetoothDevice? _currentDevice;
    private ulong _currentDeviceAddress; // 用于重连，避免 IBluetoothDevice 无地址属性
    private IRcspProtocol? _protocol;
    private byte[]? _firmwareData;
    private int _sentBytes;
    private readonly Stopwatch _speedWatch = new();
    private bool _disposed;

    private DateTime? _lastRequestTime; // 最后一次请求时间
    private byte? _lastRequestSn;       // 最后一次请求的 Sn
    private const int MinSameCmdE5TimeMs = 50; // 最小重复命令间隔（毫秒）

    // 超时管理：对应小程序SDK的 J()、V()、F()、M()、P()、gt() 方法
    private CancellationTokenSource? _commandTimeoutCts;  // 命令响应超时 (J/V)
    private CancellationTokenSource? _offlineTimeoutCts;  // 设备离线等待超时 (P/M)
    private CancellationTokenSource? _reconnectTimeoutCts; // 重连超时 (gt/F)

    // 重连状态管理（对应小程序SDK的 this.o 和相关标记）
    private bool _isWaitingForReconnect; // 是否正在等待重连（对应 SDK 中 this.o != null）
    private ReconnectInfo? _reconnectInfo; // 重连信息

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
        _readyStrategy = new NoopReadyToReconnectStrategy();
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
            var selected = _bleService.GetDiscoveredDevices()
                .FirstOrDefault(d => d.DeviceId == deviceId);

            _currentDevice = selected; // BleDevice 实现了 IBluetoothDevice
            _currentDeviceAddress = selected?.BluetoothAddress ?? 0UL;

            if (_currentDevice == null)
            {
                return CreateErrorResult(-1, "未找到指定设备");
            }

            var connected = await _currentDevice.ConnectAsync(cancellationToken);
            if (!connected)
            {
                return CreateErrorResult(OtaErrorCode.ERROR_CONNECTION_LOST, "连接设备失败");
            }

            // 监听设备连接状态变更（对应小程序SDK的 onDeviceDisconnect）
            _currentDevice.ConnectionStatusChanged += OnDeviceConnectionStatusChanged;

            XTrace.WriteLine($"[OtaManager] 设备连接成功: {_currentDevice.DeviceName}");

            // 3. 初始化协议（获取设备信息）
            ChangeState(OtaState.GettingDeviceInfo);
            _protocol = new RcspProtocol(_currentDevice);

            // 订阅设备请求文件块事件
            _protocol.DeviceRequestedFileBlock += OnDeviceRequestedFileBlock;

            _deviceInfo = await _protocol.InitializeAsync(deviceId, cancellationToken);
            XTrace.WriteLine($"[OtaManager] 设备信息: {_deviceInfo}");

            // 4. 查询是否可更新
            ChangeState(OtaState.GettingDeviceInfo);
            var canUpdate = await _protocol.InquireCanUpdateAsync(cancellationToken);
            if (!canUpdate.CanUpdate)
            {
                return CreateErrorResult(-1, $"设备不支持更新: {canUpdate}");
            }

            XTrace.WriteLine("[OtaManager] 设备支持更新");

            // ⚠️ 4.5. 根据设备信息决定升级流程 (对应小程序SDK的 H() 方法)
            // 决策树:
            //   if (isSupportDoubleBackup) → enterUpdateMode + startTransfer
            //   else if (isNeedBootLoader) → changeReceiveMtu + startCommandTimeout + wait
            //   else if (isMandatoryUpgrade) → enterUpdateMode + startTransfer
            //   else → readyToReconnectDevice
            bool needEnterUpdateMode;

            if (_deviceInfo.IsSupportDoubleBackup)
            {
                XTrace.WriteLine("[OtaManager] 设备支持双备份模式");
                
                // 对应 SDK: this.st(null) - 双备份模式不需要重连，清空重连信息
                _reconnectInfo = null;
                _isWaitingForReconnect = false;
                
                needEnterUpdateMode = true;
            }
            else if (_deviceInfo.IsNeedBootLoader)
            {
                XTrace.WriteLine("[OtaManager] 设备需要 BootLoader 模式");
                // 与小程序 SDK 一致：进入 BootLoader 需要调整接收 MTU，以适配后续传输
                try
                {
                    if (_currentDevice != null)
                    {
                        // 在 Windows 下协商 MTU，默认请求较大值，具体结果由平台决定
                        if (selected != null)
                        {
                            var mtu = await _bleService.NegotiateMtuAsync(selected);
                            XTrace.WriteLine($"[OtaManager] BootLoader 模式，已协商 MTU={mtu}");
                        }
                        else
                        {
                            XTrace.WriteLine("[OtaManager] 当前设备不是 BleDevice，跳过 MTU 协商");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // MTU 协商失败不阻断流程，仅记录日志（与 SDK 的容错一致）
                    XTrace.WriteLine($"[OtaManager] MTU 协商失败: {ex.Message}");
                }
                // ⚠️ 与 SDK 保持一致：BootLoader 模式只启动命令超时，不启动离线等待超时
                // SDK: this.A.changeReceiveMtu(), this.J()
                needEnterUpdateMode = false;
                StartCommandTimeout(); // 启动命令超时监控
            }
            else if (_deviceInfo.IsMandatoryUpgrade)
            {
                XTrace.WriteLine("[OtaManager] 设备强制升级模式");
                needEnterUpdateMode = true;
            }
            else
            {
                XTrace.WriteLine("[OtaManager] 设备普通升级模式 (需要重连)");
                
                // 设置重连信息（对应 SDK 的 this.st(t)）
                _reconnectInfo = new ReconnectInfo
                {
                    DeviceAddress = _currentDeviceAddress,
                    UseNewMacMethod = true
                };
                _isWaitingForReconnect = true;

                // 🔥 P1 修复：完全事件驱动，不同步等待
                // 对应 SDK：it() 立即返回，重连由 onDeviceDisconnect → onNeedReconnect 事件链触发
                
                // 调用 it() 准备重连，启动 6 秒离线等待
                await ReadyToReconnectDeviceAsync(cancellationToken);
                
                XTrace.WriteLine("[OtaManager] ✅ 已启动重连准备（it()），立即返回");
                XTrace.WriteLine("[OtaManager] 后续流程将由设备断开事件触发（HandleReconnectCompleteAsync）");
                
                // 🎯 完全事件驱动：it() 后立即返回成功
                // 设备断开并重连后，OnDeviceConnectionStatusChanged 会调用 HandleReconnectCompleteAsync
                // HandleReconnectCompleteAsync 将继续执行：读取偏移 → 进入更新模式 → 传输文件
                
                _totalTimeWatch.Stop();
                return new OtaResult
                {
                    Success = true,
                    ErrorCode = 0,
                    ErrorMessage = "单备份OTA已启动，等待设备重连（事件驱动模式）",
                    DeviceInfo = _deviceInfo,
                    FinalState = OtaState.WaitingReconnect,
                    TotalTime = _totalTimeWatch.Elapsed
                };
            }

            // 5. 读取文件偏移（断点续传）
            ChangeState(OtaState.ReadingFileOffset);
            var fileOffset = await _protocol.ReadFileOffsetAsync(cancellationToken);
            _sentBytes = (int)fileOffset.Offset;

            if (_sentBytes > 0)
            {
                XTrace.WriteLine($"[OtaManager] 检测到断点续传，从偏移 {_sentBytes} 开始");
            }

            // 6. 进入更新模式 (仅在需要时)
            if (needEnterUpdateMode)
            {
                ChangeState(OtaState.EnteringUpdateMode);
                var enterSuccess = await _protocol.EnterUpdateModeAsync(cancellationToken);
                if (!enterSuccess)
                {
                    return CreateErrorResult(OtaErrorCode.ERROR_OTA_FAIL, "进入更新模式失败");
                }

                XTrace.WriteLine("[OtaManager] 已进入更新模式");
                
                // 对应 SDK N() 方法：成功后启动命令超时（对应 t.J()）
                StartCommandTimeout();
            }

            // 7. 开始传输固件数据
            // 对应 SDK：进入更新模式后，等待设备主动请求文件块（通过 CmdReadFileBlock）
            // 设备也可能主动通知文件大小（通过 CmdNotifyUpdateFileSize）
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

                // 启动重连超时计时（对应小程序SDK的 gt()）
                StartReconnectTimeout();

                var currentDevice = _currentDevice;
                if (currentDevice == null)
                {
                    // 清理重连超时（对应小程序SDK的 F()）
                    ClearReconnectTimeout();
                    return CreateErrorResult(OtaErrorCode.ERROR_CONNECTION_LOST, "设备对象为空，无法等待重连");
                }

                var reconnectedDevice = await _reconnectService.WaitForReconnectAsync(
                    _currentDeviceAddress,
                    useNewMacMethod: true,
                    timeoutMs: Config.ReconnectTimeout,
                    cancellationToken: cancellationToken);

                if (reconnectedDevice == null)
                {
                    // 清理重连超时（对应小程序SDK的 F()）
                    ClearReconnectTimeout();
                    return CreateErrorResult(OtaErrorCode.ERROR_RECONNECT_TIMEOUT, "设备重连超时");
                }

                if (reconnectedDevice != null)
                {
                    XTrace.WriteLine($"[OtaManager] 设备重连成功: {reconnectedDevice.DeviceName}");
                    // 清理重连超时（对应小程序SDK的 F()）
                    ClearReconnectTimeout();
                }
            }

            // 10. 完成
            ChangeState(OtaState.Completed);
            _totalTimeWatch.Stop();
            
            // ⚠️ 设置进度为100% (对应小程序SDK的 this.W(100))
            _progress = new OtaProgress
            {
                TotalBytes = _firmwareData?.Length ?? 0,
                TransferredBytes = _firmwareData?.Length ?? 0,
                State = OtaState.Completed
            };
            ProgressChanged?.Invoke(this, _progress);
            
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

    /// <summary>
    /// 进入“准备重连”阶段的最小骨架（对应小程序 SDK 的 it()）：
    /// 仅记录日志并保持时序对齐，真正的重连超时在进入等待重连阶段时开启。
    /// </summary>
    /// <summary>
    /// 准备进入重连阶段（对应小程序 SDK it()）。
    /// 1) 调用策略扩展点执行设备族/模式特定动作；
    /// 2) 可选：根据配置主动断开当前连接以加速重连（默认关闭）；
    /// 重连超时由 WaitingReconnect 阶段统一管理。
    /// </summary>
    private async Task ReadyToReconnectDeviceAsync(CancellationToken cancellationToken)
    {
        XTrace.WriteLine("[OtaManager] 准备进入重连阶段（it()）");

        // 🔥 P0 修复1: 对应 SDK it() 内部的 this.P(6000)
        // SDK 逻辑：启动 6 秒离线等待超时（在 onDeviceDisconnect 中清除）
        StartOfflineWaitTimeout(async () =>
        {
            XTrace.WriteLine("[OtaManager] 设备离线等待超时（P超时），触发重连流程");
            
            // 对应 SDK P() 超时回调的完整逻辑：
            // e.i=0,e.l=0;         - 重置进度（C#在 CleanupResources 中统一处理）
            // const t=e.o.copy();  - 复制重连信息
            // e.Rt(t),             - 触发 onNeedReconnect
            // e.gt(t),             - 启动重连超时
            // e.st(null)           - 清空重连信息
            
            if (_reconnectInfo != null)
            {
                var reconnectInfo = _reconnectInfo.Copy();  // 复制重连信息
                _reconnectInfo = null;                       // 清空重连信息
                _isWaitingForReconnect = false;
                
                StartReconnectTimeout();  // 启动重连超时
                
                // 触发重连流程（对应 onNeedReconnect）
                await TriggerReconnectFlowAsync(reconnectInfo);
            }
            else
            {
                XTrace.WriteLine("[OtaManager] P超时但无重连信息，可能已处理");
            }
        });

        if (_currentDevice != null && _protocol != null && _deviceInfo != null)
        {
            // 🔥 P0 修复2: 对应 SDK it() 中的 this.A.changeCommunicationWay()
            // 告知设备切换通信方式和是否支持新的重启广播方式
            try
            {
                byte communicationWay = _deviceInfo.CommunicationWay;
                bool isSupportNewRebootWay = _deviceInfo.IsSupportNewRebootWay;
                
                XTrace.WriteLine($"[OtaManager] 发送切换通信方式命令: way={communicationWay}, newReboot={isSupportNewRebootWay}");
                var result = await _protocol.ChangeCommunicationWayAsync(communicationWay, isSupportNewRebootWay, cancellationToken);
                
                // 对应SDK: onResult(e){ t.isSupportNewReconnectADV=0!=e }
                // 结果用于设置是否支持新的重连广播方式
                bool isSupportNewReconnectADV = result != 0;
                if (_reconnectInfo != null)
                {
                    _reconnectInfo.UseNewMacMethod = isSupportNewReconnectADV;
                }
                
                XTrace.WriteLine($"[OtaManager] 切换通信方式命令已发送，支持新广播: {isSupportNewReconnectADV}");
            }
            catch (Exception ex)
            {
                // 对应SDK的错误处理逻辑：
                // onError(t,s){ t!=h.ERROR_REPLY_BAD_STATUS&&t!=h.ERROR_REPLY_BAD_RESULT||e.D(t,s) }
                // 
                // 真实含义（JavaScript逻辑运算符优先级）：
                // if (t != BAD_STATUS && t != BAD_RESULT) {
                //     // 忽略错误，不处理
                // } else {
                //     e.D(t,s)  // 只有BAD_STATUS或BAD_RESULT才报错
                // }
                //
                // 所以SDK的逻辑是：只有BAD_STATUS/BAD_RESULT才会报错，其他错误都忽略！
                // 这与RcspProtocol中捕获所有异常返回0的实现一致
                
                // ✅ 修复：所有异常都忽略，因为RcspProtocol已经处理了
                XTrace.WriteLine($"[OtaManager] 切换通信方式异常（SDK逻辑：忽略所有错误）: {ex.Message}");
                // 继续执行，不中断流程
            }

            // 设备族/模式特定策略（默认 No-Op）
            try
            {
                await _readyStrategy.ExecuteAsync(_currentDevice, Config, cancellationToken);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine($"[OtaManager] it() 策略执行异常: {ex.Message}");
            }

            // 可选断开：部分设备在 SDK it() 中会主动断开以加速切换
            if (Config.EnableReadyReconnectDisconnect)
            {
                try
                {
                    XTrace.WriteLine("[OtaManager] it() 启用：主动断开当前连接以准备重连");
                    await _currentDevice.DisconnectAsync();
                }
                catch (Exception ex)
                {
                    XTrace.WriteLine($"[OtaManager] 主动断开异常: {ex.Message}");
                }
            }
        }
        else
        {
            XTrace.WriteLine($"[OtaManager] ⚠️ 无法发送ChangeCommunicationWay: device={_currentDevice != null}, protocol={_protocol != null}, deviceInfo={_deviceInfo != null}");
        }
    }

    /// <summary>设置自定义的准备重连策略（测试或特定机型可注入）</summary>
    internal void SetReadyToReconnectStrategy(IReadyToReconnectStrategy strategy)
    {
        _readyStrategy = strategy ?? new NoopReadyToReconnectStrategy();
    }

    /// <summary>处理重连完成后的逻辑（对应小程序SDK的 onDeviceInit）</summary>
    private async Task HandleReconnectCompleteAsync()
    {
        XTrace.WriteLine("[OtaManager] 🔥 处理重连完成逻辑（单备份OTA事件驱动继续）");

        // 对应 SDK: if (this.isOTA() && null != this.T)
        // 此时 _reconnectTimeoutCts 已在 StartReconnectTimeout 中创建
        
        // 获取设备信息（对应 SDK 的 onDeviceInit 参数）
        if (_protocol == null || _currentDevice == null)
        {
            XTrace.WriteLine("[OtaManager] 协议或设备为空，无法继续");
            ChangeState(OtaState.Failed);
            ErrorOccurred?.Invoke(OtaErrorCode.ERROR_OTA_FAIL, "协议或设备为空");
            return;
        }

        if (_firmwareData == null)
        {
            XTrace.WriteLine("[OtaManager] 固件数据为空，无法继续");
            ChangeState(OtaState.Failed);
            ErrorOccurred?.Invoke(OtaErrorCode.ERROR_OTA_FAIL, "固件数据为空");
            return;
        }

        try
        {
            // 重新初始化协议并获取设备信息
            XTrace.WriteLine("[OtaManager] 重连后重新初始化协议...");
            var deviceInfo = await _protocol.InitializeAsync(_currentDevice.DeviceId, default);
            _deviceInfo = deviceInfo;

            // 🔥 单备份OTA重连后，继续完整流程：读取偏移 → 进入更新模式 → 传输文件
            
            // 1. 读取文件偏移（断点续传）
            ChangeState(OtaState.ReadingFileOffset);
            XTrace.WriteLine("[OtaManager] 读取文件偏移...");
            var fileOffset = await _protocol.ReadFileOffsetAsync(default);
            _sentBytes = (int)fileOffset.Offset;

            if (_sentBytes > 0)
            {
                XTrace.WriteLine($"[OtaManager] 检测到断点续传，从偏移 {_sentBytes} 开始");
            }

            // 2. 进入更新模式（对应 SDK：重连后强制升级或需要进入更新模式）
            bool needEnterUpdateMode = deviceInfo.IsMandatoryUpgrade || deviceInfo.IsNeedBootLoader;
            
            if (needEnterUpdateMode)
            {
                ChangeState(OtaState.EnteringUpdateMode);
                XTrace.WriteLine("[OtaManager] 进入更新模式...");
                var enterSuccess = await _protocol.EnterUpdateModeAsync(default);
                if (!enterSuccess)
                {
                    XTrace.WriteLine("[OtaManager] 进入更新模式失败");
                    ChangeState(OtaState.Failed);
                    ErrorOccurred?.Invoke(OtaErrorCode.ERROR_OTA_FAIL, "进入更新模式失败");
                    return;
                }
                XTrace.WriteLine("[OtaManager] 已进入更新模式");
            }

            // 3. 通知文件大小
            ChangeState(OtaState.EnteringUpdateMode);
            XTrace.WriteLine($"[OtaManager] 通知文件大小: {_firmwareData.Length} bytes");
            var notifySuccess = await _protocol.NotifyFileSizeAsync((uint)_firmwareData.Length, default);
            if (!notifySuccess)
            {
                XTrace.WriteLine("[OtaManager] 通知文件大小失败");
                ChangeState(OtaState.Failed);
                ErrorOccurred?.Invoke(OtaErrorCode.ERROR_OTA_FAIL, "通知文件大小失败");
                return;
            }

            // 4. 传输固件数据
            ChangeState(OtaState.TransferringFile);
            _speedWatch.Restart();
            XTrace.WriteLine("[OtaManager] 等待设备请求文件块...");

            // 等待传输完成或超时
            var transferTimeout = TimeSpan.FromMinutes(10); // 默认10分钟
            var transferTask = WaitForTransferCompleteAsync(default);
            var cts = new CancellationTokenSource(transferTimeout);
            var completedTask = await Task.WhenAny(transferTask, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token));

            if (completedTask != transferTask)
            {
                XTrace.WriteLine("[OtaManager] 固件传输超时");
                ChangeState(OtaState.Failed);
                ErrorOccurred?.Invoke(OtaErrorCode.ERROR_COMMAND_TIMEOUT, "固件传输超时");
                return;
            }

            var transferSuccess = await transferTask;
            if (!transferSuccess)
            {
                XTrace.WriteLine("[OtaManager] 固件传输失败");
                ChangeState(OtaState.Failed);
                ErrorOccurred?.Invoke(OtaErrorCode.ERROR_OTA_FAIL, "固件传输失败");
                return;
            }

            _speedWatch.Stop();
            XTrace.WriteLine("[OtaManager] ✅ 固件传输完成");

            // 5. 等待设备应用固件后重连（对应SDK的第二次重连）
            ChangeState(OtaState.WaitingReconnect);
            XTrace.WriteLine("[OtaManager] 等待设备应用固件后重连...");

            // 启动重连超时计时（对应小程序SDK的 gt()）
            StartReconnectTimeout();

            var reconnectedDevice = await _reconnectService.WaitForReconnectAsync(
                _currentDeviceAddress,
                useNewMacMethod: true,
                timeoutMs: Config.ReconnectTimeout,
                cancellationToken: default);

            // 清理重连超时（对应小程序SDK的 F()）
            ClearReconnectTimeout();

            if (reconnectedDevice == null)
            {
                XTrace.WriteLine("[OtaManager] 设备应用固件后重连超时");
                ChangeState(OtaState.Failed);
                ErrorOccurred?.Invoke(OtaErrorCode.ERROR_RECONNECT_TIMEOUT, "设备应用固件后重连超时");
                return;
            }

            _currentDevice = reconnectedDevice;
            XTrace.WriteLine($"[OtaManager] 设备应用固件后已重连: {reconnectedDevice.DeviceId}");

            // 6. 查询升级结果（对应SDK的 G() 方法）
            ChangeState(OtaState.QueryingResult);
            XTrace.WriteLine("[OtaManager] 查询升级结果...");

            var result = await _protocol.QueryUpdateResultAsync(default);
            XTrace.WriteLine($"[OtaManager] 升级结果: Status=0x{result.Status:X2}, Code=0x{result.ResultCode:X2}");

            // 对应SDK的switch(e)逻辑
            if (result.ResultCode == 0x00)  // b.nt - 成功
            {
                XTrace.WriteLine("[OtaManager] ✅ 升级成功！");
                
                // 对应SDK: t.A.rebootDevice(null) - 发送重启命令（fire-and-forget）
                try
                {
                    await _protocol.RebootDeviceAsync(default);
                }
                catch (Exception ex)
                {
                    // 重启命令失败不影响流程，设备可能已自动重启
                    XTrace.WriteLine($"[OtaManager] 发送重启命令异常（可忽略）: {ex.Message}");
                }
                
                // 对应SDK: t.v(null), t.O() - 清理配置和进度
                CleanupResources();
                
                // 对应SDK: void setTimeout((()=>{t.q()}),100) - 100ms后调用q()
                await Task.Delay(100);
                
                XTrace.WriteLine("[OtaManager] ✅✅✅ OTA 升级成功完成！");
                ChangeState(OtaState.Completed);
                _totalTimeWatch.Stop();
                
                // 设置进度为100%
                _progress = new OtaProgress
                {
                    TotalBytes = _firmwareData?.Length ?? 0,
                    TransferredBytes = _firmwareData?.Length ?? 0,
                    State = OtaState.Completed
                };
                ProgressChanged?.Invoke(this, _progress);
            }
            else if (result.ResultCode == 0x80)  // b.rt - 需要重连
            {
                XTrace.WriteLine("[OtaManager] ⚠️ 升级结果：需要再次重连（0x80）");
                
                // 对应SDK: void t.it() - 调用it()准备重连
                await ReadyToReconnectDeviceAsync(default);
                
                XTrace.WriteLine("[OtaManager] 已启动再次重连流程，等待设备断开...");
                // 后续流程将由 OnDeviceConnectionStatusChanged 触发
            }
            else
            {
                // 其他错误码
                var errorCode = result.ResultCode switch
                {
                    0x01 => OtaErrorCode.ERROR_DATA_CHECK,           // b.lt
                    0x02 => OtaErrorCode.ERROR_OTA_FAIL,             // b.ht
                    0x03 => OtaErrorCode.ERROR_ENCRYPTED_KEY_NOT_MATCH, // b.ot
                    // 可以继续添加其他错误码映射
                    _ => OtaErrorCode.ERROR_OTA_FAIL
                };
                
                XTrace.WriteLine($"[OtaManager] ❌ OTA 升级失败，结果码: 0x{result.ResultCode:X2}");
                ChangeState(OtaState.Failed);
                ErrorOccurred?.Invoke(errorCode, $"升级失败，结果码: 0x{result.ResultCode:X2}");
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine($"[OtaManager] 重连后处理异常: {ex.Message}");
            XTrace.WriteException(ex);
            ChangeState(OtaState.Failed);
            ErrorOccurred?.Invoke(OtaErrorCode.ERROR_OTA_FAIL, $"重连后处理异常: {ex.Message}");
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
    /// <summary>取消 OTA 升级（对应小程序SDK的 cancelOTA）</summary>
    public async Task<bool> CancelOtaAsync()
    {
        // 对应 SDK: if(this.U("cancelOTA")) return !1;
        if (_currentState == OtaState.Idle || _currentState == OtaState.Completed || _currentState == OtaState.Failed)
        {
            XTrace.WriteLine("[OtaManager] 当前未在 OTA 流程中，无需取消");
            return false;
        }

        // 对应 SDK: if(!this.A.isDeviceConnected())
        if (_currentDevice == null)
        {
            XTrace.WriteLine("[OtaManager] 设备未连接，取消失败");
            ErrorOccurred?.Invoke(OtaErrorCode.ERROR_CONNECTION_LOST, "设备未连接");
            return false;
        }

        // 对应 SDK: if(null!=this.u&&this.u.isSupportDoubleBackup)
        if (_deviceInfo != null && _deviceInfo.IsSupportDoubleBackup)
        {
            XTrace.WriteLine("[OtaManager] 双备份模式，发送退出更新模式命令");
            
            try
            {
                if (_protocol != null)
                {
                    // 对应 SDK: this.A.exitUpdateMode(e)
                    // 注意：当前 IRcspProtocol 可能还没有 ExitUpdateModeAsync 方法
                    // 暂时使用通用错误码触发取消
                    XTrace.WriteLine("[OtaManager] TODO: 需要实现 ExitUpdateModeAsync 协议方法");
                }
                
                ChangeState(OtaState.Failed);
                CleanupResources();
                return true;
            }
            catch (Exception ex)
            {
                XTrace.WriteLine($"[OtaManager] 退出更新模式异常: {ex.Message}");
                ChangeState(OtaState.Failed);
                CleanupResources();
                return true;  // SDK 的 onResult 和 onError 都会调用 S()，所以无论如何都返回 true
            }
        }

        // 对应 SDK: 单备份模式不能中断
        XTrace.WriteLine("[OtaManager] 单备份模式，OTA 进程不能被中断");
        return false;
    }

    /// <summary>处理设备连接状态变更事件（对应小程序SDK的 onDeviceDisconnect）</summary>
    private async void OnDeviceConnectionStatusChanged(object? sender, bool isConnected)
    {
        // 仅处理断开连接事件
        if (isConnected || _currentState == OtaState.Idle || _currentState == OtaState.Completed || _currentState == OtaState.Failed)
        {
            return;
        }

        XTrace.WriteLine("[OtaManager] 检测到设备断开连接");

        // 对应小程序SDK的 onDeviceDisconnect() 逻辑
        if (_isWaitingForReconnect && _reconnectInfo != null)
        {
            XTrace.WriteLine("[OtaManager] 设备离线，准备重连");

            // this.M() - 清除离线等待超时
            ClearOfflineWaitTimeout();

            // null==this.T - 如果重连超时未启动
            if (_reconnectTimeoutCts == null)
            {
                // this.P(300) - 启动 300ms 后处理
                await Task.Delay(300);

                // 触发重连流程（对应 SDK 的 onNeedReconnect 回调）
                var reconnectInfo = _reconnectInfo.Copy();
                _isWaitingForReconnect = false;
                _reconnectInfo = null;

                // 启动重连超时（对应 SDK 的 gt()）
                StartReconnectTimeout();

                // 执行重连流程
                await TriggerReconnectFlowAsync(reconnectInfo);
            }
        }
        else
        {
            // 没有重连信息，报错
            XTrace.WriteLine("[OtaManager] 设备离线且无重连信息");
            ChangeState(OtaState.Failed);
        }
    }

    /// <summary>触发重连流程（对应 SDK 的 onNeedReconnect + WaitForReconnectAsync）</summary>
    private async Task TriggerReconnectFlowAsync(ReconnectInfo reconnectInfo)
    {
        try
        {
            var reconnectedDevice = await _reconnectService.WaitForReconnectAsync(
                reconnectInfo.DeviceAddress,
                useNewMacMethod: reconnectInfo.UseNewMacMethod,
                timeoutMs: Config.ReconnectTimeout,
                cancellationToken: default);

            if (reconnectedDevice != null)
            {
                _currentDevice = reconnectedDevice;
                _currentDeviceAddress = reconnectedDevice.BluetoothAddress;
                
                var connected = await _currentDevice.ConnectAsync();
                if (connected)
                {
                    XTrace.WriteLine($"[OtaManager] 设备重连成功: {reconnectedDevice.DeviceName}");
                    
                    // 清除重连超时（对应 SDK 的 F()）
                    ClearReconnectTimeout();

                    // 处理重连后逻辑（对应 SDK 的 onDeviceInit）
                    await HandleReconnectCompleteAsync();
                }
                else
                {
                    XTrace.WriteLine("[OtaManager] 重连后连接失败");
                    ClearReconnectTimeout();
                }
            }
            else
            {
                XTrace.WriteLine("[OtaManager] 重连超时");
                ClearReconnectTimeout();
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine($"[OtaManager] 重连异常: {ex.Message}");
            ClearReconnectTimeout();
        }
    }

    /// <summary>处理设备请求文件块事件</summary>
    protected internal async void OnDeviceRequestedFileBlock(object? sender, RcspPacket packet)
    {
        if (_firmwareData == null || _currentDevice == null || _currentState != OtaState.TransferringFile)
        {
            return;
        }

        try
        {
            // ⚠️ 收到设备命令，清除之前的超时 (对应小程序SDK的 V() 方法)
            ClearCommandTimeout();

            // 解析请求：Sn (1 byte) + offset (4 bytes) + length (2 bytes)
            if (packet.Payload.Length < 7)
            {
                XTrace.WriteLine("[OtaManager] 无效的文件块请求");
                return;
            }

            var sn = packet.Payload[0]; // 获取序列号
            var offset = BitConverter.ToInt32(packet.Payload, 1); // 从索引1开始读取offset
            var length = BitConverter.ToUInt16(packet.Payload, 5); // 从索引5开始读取length

            // ⚠️ 重复命令过滤：和小程序SDK保持一致
            var now = DateTime.Now;
            if (_lastRequestSn == sn && _lastRequestTime.HasValue)
            {
                var elapsed = (now - _lastRequestTime.Value).TotalMilliseconds;
                if (elapsed < MinSameCmdE5TimeMs)
                {
                    XTrace.WriteLine($"[OtaManager] 忽略重复命令: Sn={sn}, elapsed={elapsed}ms");
                    return;
                }
            }
            _lastRequestSn = sn;
            _lastRequestTime = now;

            // ⚠️ 特殊情况:offset=0 && len=0 表示查询更新结果，不是文件块请求
            if (offset == 0 && length == 0)
            {
                XTrace.WriteLine("[OtaManager] 收到查询更新结果信号 (offset=0, len=0)");

                // 先以零数据块应答设备请求 (与 SDK 行为一致：先快速 ACK 再查询结果)
                var zeroAckPayload = new byte[1 + 1 + 4 + 2]; // Status(1)+Sn(1)+offset(4)+len(2)
                zeroAckPayload[0] = 0x00; // STATUS_SUCCESS
                zeroAckPayload[1] = sn;   // 使用当前请求中的 Sn 即可
                // offset/len 已经是 0

                var zeroAckPacket = new RcspPacket
                {
                    Flag = 0x00, // 响应
                    OpCode = OtaOpCode.CMD_OTA_FILE_BLOCK,
                    Payload = zeroAckPayload
                };
                await _currentDevice.WriteAsync(zeroAckPacket.ToBytes());

                // 启动新的命令超时 (对应小程序SDK的 J())
                StartCommandTimeout();

                // 查询升级结果 (对应小程序SDK的 G())
                try
                {
                    if (_protocol is IRcspProtocol proto)
                    {
                        var rsp = await proto.QueryUpdateResultAsync();
                        XTrace.WriteLine($"[OtaManager] 升级结果查询: Status=0x{rsp.Status:X2}, Code={(rsp is RspUpdateResult ur ? ur.ResultCode : (byte)0xFF)}");
                    }
                }
                catch (Exception ex)
                {
                    // 查询失败不阻断流程，继续进入等待重连
                    XTrace.WriteLine($"[OtaManager] 升级结果查询失败: {ex.Message}");
                }

                // 认定传输阶段已完成：推进 sentBytes=Total，触发 WaitForTransferComplete 退出
                if (_firmwareData != null)
                {
                    _sentBytes = _firmwareData.Length;
                    UpdateProgress();
                }

                return;
            }

            // 从缓存中获取原始命令包（包含正确的 Sn）
            var cachedCommand = (_protocol as RcspProtocol)?.GetCachedDeviceCommand(offset, length) ?? packet;
            if (cachedCommand == packet)
            {
                XTrace.WriteLine($"[OtaManager] 警告: 未找到缓存的命令 offset={offset}, len={length}，使用当前packet");
            }
            
            var cachedSn = cachedCommand.Payload[0]; // 从缓存的命令中获取正确的 Sn

            // 读取文件块
            var block = _fileService.ReadFileBlock(_firmwareData, offset, length);

            // ⚠️ 参数验证：和小程序SDK保持一致
            byte status = 0x00; // ResponseResult.STATUS_SUCCESS
            if (block.Length == 0 && offset > 0 && length > 0)
            {
                status = 0x01; // ResponseResult.STATUS_INVALID_PARAM
                XTrace.WriteLine($"[OtaManager] 文件块读取失败: offset={offset}, len={length}");
            }

            // 构造响应：Status (1) + Sn (1) + offset (4) + length (2) + block data
            var responsePayload = new byte[1 + 1 + 4 + 2 + block.Length];
            responsePayload[0] = status;      // Status
            responsePayload[1] = cachedSn;    // 使用缓存命令中的 Sn
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

            // ⚠️ 更新进度：和小程序SDK保持一致,累加本次传输的 length (对应: t+=e, i.l=t)
            _sentBytes += block.Length;
            UpdateProgress();

            // ⚠️ 启动新的命令超时 (对应小程序SDK的 J() 方法)
            StartCommandTimeout();

            XTrace.WriteLine($"[OtaManager] 发送文件块: offset={offset}, length={block.Length}, 进度={_progress.Percentage}%");
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            ErrorOccurred?.Invoke(OtaErrorCode.ERROR_OTA_FAIL, $"发送文件块失败: {ex.Message}");
        }
    }

    /// <summary>测试注入：仅用于单元测试，注入设备、协议与固件数据，并设置状态</summary>
    protected internal void TestInject(IBluetoothDevice device, IRcspProtocol protocol, byte[] firmwareData, OtaState state = OtaState.TransferringFile)
    {
        _currentDevice = device;
        _protocol = protocol;
        _firmwareData = firmwareData;
        _currentState = state;
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
        // ⚠️ 清理所有超时计时器 (对应小程序SDK的 bt() 方法)
        ClearAllTimeouts();

        // ⚠️ 重置进度 (对应小程序SDK的 O() 方法: this.i=0, this.l=0)
        _sentBytes = 0;

        if (_protocol != null)
        {
            _protocol.DeviceRequestedFileBlock -= OnDeviceRequestedFileBlock;
            if (_protocol is IDisposable disp)
            {
                disp.Dispose();
            }
            _protocol = null;
        }

        _currentDevice = null;
        _firmwareData = null;
        _speedWatch.Reset();
    }

    /// <summary>启动命令响应超时 (对应小程序SDK的 J() 方法)</summary>
    private void StartCommandTimeout()
    {
        ClearCommandTimeout(); // 先清除旧超时 (对应 V() 方法)
        
        _commandTimeoutCts = new CancellationTokenSource();
        Task.Delay(Config.CommandTimeout, _commandTimeoutCts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                XTrace.WriteLine("[OtaManager] 命令响应超时");
                ErrorOccurred?.Invoke(OtaErrorCode.ERROR_COMMAND_TIMEOUT, "命令响应超时");
            }
        });
    }

    /// <summary>清除命令响应超时 (对应小程序SDK的 V() 方法)</summary>
    private void ClearCommandTimeout()
    {
        _commandTimeoutCts?.Cancel();
        _commandTimeoutCts?.Dispose();
        _commandTimeoutCts = null;
    }

    /// <summary>启动设备离线等待超时 (对应小程序SDK的 P() 方法)</summary>
    private void StartOfflineWaitTimeout(Func<Task> onTimeoutAsync)
    {
        ClearOfflineWaitTimeout(); // 先清除旧超时 (对应 M() 方法)
        
        _offlineTimeoutCts = new CancellationTokenSource();
        Task.Delay(Config.OfflineTimeout, _offlineTimeoutCts.Token).ContinueWith(async t =>
        {
            if (!t.IsCanceled)
            {
                XTrace.WriteLine("[OtaManager] 设备离线等待超时，触发重连");
                if (onTimeoutAsync != null)
                {
                    await onTimeoutAsync();
                }
            }
        });
    }

    /// <summary>清除设备离线等待超时 (对应小程序SDK的 M() 方法)</summary>
    private void ClearOfflineWaitTimeout()
    {
        _offlineTimeoutCts?.Cancel();
        _offlineTimeoutCts?.Dispose();
        _offlineTimeoutCts = null;
    }

    /// <summary>启动重连超时 (对应小程序SDK的 gt() 方法)</summary>
    private void StartReconnectTimeout()
    {
        ClearReconnectTimeout(); // 先清除旧超时 (对应 F() 方法)
        
        _reconnectTimeoutCts = new CancellationTokenSource();
        Task.Delay(Config.ReconnectTimeout, _reconnectTimeoutCts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                XTrace.WriteLine("[OtaManager] 重连超时");
                ErrorOccurred?.Invoke(OtaErrorCode.ERROR_RECONNECT_TIMEOUT, "重连超时");
            }
        });
    }

    /// <summary>清除重连超时 (对应小程序SDK的 F() 方法)</summary>
    private void ClearReconnectTimeout()
    {
        _reconnectTimeoutCts?.Cancel();
        _reconnectTimeoutCts?.Dispose();
        _reconnectTimeoutCts = null;
    }

    /// <summary>清除所有超时 (对应小程序SDK的 bt() 方法)</summary>
    private void ClearAllTimeouts()
    {
        ClearReconnectTimeout();    // F()
        ClearCommandTimeout();       // V()
        ClearOfflineWaitTimeout();   // M()
    }

    public void Dispose()
    {
        if (_disposed) return;

        CleanupResources();
        _disposed = true;

        XTrace.WriteLine("[OtaManager] 已释放资源");
    }
}
