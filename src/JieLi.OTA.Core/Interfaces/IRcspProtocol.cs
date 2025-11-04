using JieLi.OTA.Core.Protocols;
using JieLi.OTA.Core.Protocols.Responses;

namespace JieLi.OTA.Core.Interfaces;

/// <summary>RCSP 协议接口</summary>
public interface IRcspProtocol
{
    /// <summary>初始化协议（获取设备信息）</summary>
    /// <param name="deviceId">设备 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>设备信息</returns>
    Task<RspDeviceInfo> InitializeAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>查询是否可更新</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新查询响应</returns>
    Task<RspCanUpdate> InquireCanUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>读取文件偏移</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件偏移响应</returns>
    Task<RspFileOffset> ReadFileOffsetAsync(CancellationToken cancellationToken = default);

    /// <summary>进入更新模式</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> EnterUpdateModeAsync(CancellationToken cancellationToken = default);

    /// <summary>通知文件大小</summary>
    /// <param name="fileSize">文件大小</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> NotifyFileSizeAsync(uint fileSize, CancellationToken cancellationToken = default);

    /// <summary>发送命令并等待响应</summary>
    /// <typeparam name="TResponse">响应类型</typeparam>
    /// <param name="command">命令</param>
    /// <param name="timeoutMs">超时时间（毫秒）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应</returns>
    Task<TResponse> SendCommandAsync<TResponse>(RcspCommand command, int timeoutMs = 5000, CancellationToken cancellationToken = default) 
        where TResponse : RcspResponse, new();

    /// <summary>设备请求文件块事件</summary>
    event EventHandler<RcspPacket>? DeviceRequestedFileBlock;

    /// <summary>断开连接</summary>
    Task DisconnectAsync();
}
