namespace JieLi.OTA.Core.Models;

/// <summary>OTA 配置</summary>
public class OtaConfig
{
    /// <summary>命令响应超时时间（毫秒），对应小程序SDK的 WAITING_CMD_TIMEOUT</summary>
    /// <remarks>小程序SDK值: 20000ms (20秒)</remarks>
    public int CommandTimeout { get; set; } = 20000;

    /// <summary>重连超时时间（毫秒），对应小程序SDK的 RECONNECT_DEVICE_TIMEOUT</summary>
    /// <remarks>小程序SDK值: 80000ms (80秒)</remarks>
    public int ReconnectTimeout { get; set; } = 80000;

    /// <summary>等待设备离线超时时间（毫秒），对应小程序SDK的 WAITING_DEVICE_OFFLINE_TIMEOUT</summary>
    /// <remarks>小程序SDK值: 6000ms (6秒)</remarks>
    public int OfflineTimeout { get; set; } = 6000;

    /// <summary>最大重试次数</summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>传输块大小（字节）</summary>
    public int TransferBlockSize { get; set; } = 512;

    /// <summary>发送延迟（毫秒）</summary>
    public int SendDelay { get; set; } = 0;

    /// <summary>是否启用日志</summary>
    public bool EnableLogging { get; set; } = true;
}
