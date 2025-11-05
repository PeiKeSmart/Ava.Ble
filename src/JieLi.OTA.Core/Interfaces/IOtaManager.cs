using JieLi.OTA.Core.Models;
using JieLi.OTA.Core.Protocols.Responses;

namespace JieLi.OTA.Core.Interfaces;

/// <summary>OTA 管理器接口</summary>
public interface IOtaManager
{
    /// <summary>开始 OTA 升级</summary>
    /// <param name="deviceId">设备 ID</param>
    /// <param name="firmwareFilePath">固件文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>升级结果</returns>
    Task<OtaResult> StartOtaAsync(string deviceId, string firmwareFilePath, CancellationToken cancellationToken = default);

    /// <summary>取消 OTA 升级</summary>
    /// <returns>是否成功取消（双备份模式返回true，单备份模式返回false）</returns>
    Task<bool> CancelOtaAsync();

    /// <summary>OTA 进度事件</summary>
    event EventHandler<OtaProgress>? ProgressChanged;

    /// <summary>OTA 状态变更事件</summary>
    event EventHandler<OtaState>? StateChanged;
}

/// <summary>OTA 结果</summary>
public class OtaResult
{
    /// <summary>是否成功</summary>
    public bool Success { get; set; }

    /// <summary>错误码</summary>
    public int ErrorCode { get; set; }

    /// <summary>错误消息</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>设备信息</summary>
    public RspDeviceInfo? DeviceInfo { get; set; }

    /// <summary>最终状态</summary>
    public OtaState FinalState { get; set; }

    /// <summary>总用时</summary>
    public TimeSpan TotalTime { get; set; }
}
