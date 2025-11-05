using JieLi.OTA.Core.Interfaces;
using JieLi.OTA.Core.Protocols;
using NewLife.Log;
using System.Collections.Concurrent;

namespace JieLi.OTA.Infrastructure.Bluetooth;

/// <summary>RCSP 数据处理器</summary>
/// <remarks>
/// 负责 RCSP 命令发送、响应接收、超时管理。
/// </remarks>
public class RcspDataHandler : IDisposable
{
    private readonly IBluetoothDevice _device;
    private readonly RcspParser _parser = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<RcspPacket>> _pendingCommands = new(); // Key = (OpCode << 16) | Sn
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    private byte _currentSn = 0;
    private bool _disposed;

    public RcspDataHandler(IBluetoothDevice device)
    {
        _device = device;
    }

    /// <summary>生成序列号（0-255 循环）</summary>
    private byte GenerateSn()
    {
        var sn = _currentSn;
        _currentSn = (byte)((_currentSn + 1) % 256);
        return sn;
    }

    /// <summary>生成复合 Key: (OpCode << 16) | Sn</summary>
    private static int MakeKey(byte opCode, byte sn) => (opCode << 16) | sn;

    /// <summary>初始化（订阅数据接收）</summary>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        return await _device.SubscribeNotifyAsync(OnDataReceived, cancellationToken);
    }

    /// <summary>发送命令并等待响应</summary>
    /// <typeparam name="TResponse">响应类型</typeparam>
    /// <param name="command">命令</param>
    /// <param name="timeoutMs">超时时间（毫秒）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应对象</returns>
    public async Task<TResponse> SendCommandAsync<TResponse>(
        RcspCommand command,
        int timeoutMs = 5000,
        CancellationToken cancellationToken = default)
        where TResponse : RcspResponse, new()
    {
        await _sendSemaphore.WaitAsync(cancellationToken);

        try
        {
            // 生成序列号
            byte sn = GenerateSn();
            
            // 创建数据包 (Payload 格式: [Sn, ...业务数据])
            var packet = command.ToPacket(sn);

            // 生成复合 Key: (OpCode << 16) | Sn
            int key = MakeKey(command.OpCode, sn);

            // 创建响应等待任务
            var tcs = new TaskCompletionSource<RcspPacket>();
            _pendingCommands[key] = tcs;

            // 发送数据
            var bytes = packet.ToBytes();
            XTrace.WriteLine($"[RcspDataHandler] 发送命令: OpCode=0x{command.OpCode:X2}, Sn={sn}, Length={bytes.Length}");

            bool sent = await _device.WriteAsync(bytes, cancellationToken);
            if (!sent)
            {
                _pendingCommands.TryRemove(key, out _);
                throw new IOException("发送命令失败");
            }

            // 等待响应或超时
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                linkedCts.Token.Register(() => tcs.TrySetCanceled());
                var responsePacket = await tcs.Task;

                // 解析响应
                var response = new TResponse();
                response.FromPacket(responsePacket);

                XTrace.WriteLine($"[RcspDataHandler] 收到响应: OpCode=0x{response.OpCode:X2}, Sn={response.Sn}, Status={response.Status}");
                return response;
            }
            catch (OperationCanceledException)
            {
                _pendingCommands.TryRemove(key, out _);

                if (timeoutCts.Token.IsCancellationRequested)
                {
                    throw new TimeoutException($"命令超时: OpCode=0x{command.OpCode:X2}, Sn={sn}, Timeout={timeoutMs}ms");
                }

                throw;
            }
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    /// <summary>发送数据（不等待响应）</summary>
    public async Task<bool> SendDataAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        await _sendSemaphore.WaitAsync(cancellationToken);

        try
        {
            XTrace.WriteLine($"[RcspDataHandler] 发送数据: {data.Length} bytes");
            return await _device.WriteAsync(data, cancellationToken);
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    private void OnDataReceived(byte[] data)
    {
        try
        {
            XTrace.WriteLine($"[RcspDataHandler] 接收数据: {data.Length} bytes - {BitConverter.ToString(data).Replace("-", " ")}");

            // 添加到解析器
            _parser.AddData(data);

            // 尝试解析数据包
            while (true)
            {
                var packet = _parser.TryParse();
                if (packet == null)
                    break;

                XTrace.WriteLine($"[RcspDataHandler] 解析数据包: OpCode=0x{packet.OpCode:X2}, IsCommand={packet.IsCommand}, PayloadLen={packet.Payload.Length}");

                // 如果是响应包，从 Payload 中提取 Sn 并匹配
                if (!packet.IsCommand)
                {
                    // Response Payload: [Status, Sn, ...]
                    if (packet.Payload.Length >= 2)
                    {
                        byte sn = packet.Payload[1];
                        int key = MakeKey(packet.OpCode, sn);
                        
                        if (_pendingCommands.TryRemove(key, out var tcs))
                        {
                            XTrace.WriteLine($"[RcspDataHandler] 匹配成功: OpCode=0x{packet.OpCode:X2}, Sn={sn}");
                            tcs.TrySetResult(packet);
                        }
                        else
                        {
                            XTrace.WriteLine($"[RcspDataHandler] 未找到匹配的命令: OpCode=0x{packet.OpCode:X2}, Sn={sn}");
                        }
                    }
                    else
                    {
                        XTrace.WriteLine($"[RcspDataHandler] 响应 Payload 长度不足: OpCode=0x{packet.OpCode:X2}, Len={packet.Payload.Length}");
                    }
                }
                else
                {
                    // 处理设备主动发送的命令（如请求文件块）
                    OnDeviceCommandReceived?.Invoke(this, packet);
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    /// <summary>设备命令接收事件（设备主动发送的命令）</summary>
    public event EventHandler<RcspPacket>? OnDeviceCommandReceived;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // 取消所有等待中的命令
        foreach (var kvp in _pendingCommands)
        {
            kvp.Value.TrySetCanceled();
        }
        _pendingCommands.Clear();

        _parser.Clear();
        _sendSemaphore.Dispose();

        XTrace.WriteLine("[RcspDataHandler] 已释放");
    }
}
