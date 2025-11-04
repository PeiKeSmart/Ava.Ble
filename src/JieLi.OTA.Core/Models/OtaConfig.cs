namespace JieLi.OTA.Core.Models;

/// <summary>OTA 配置</summary>
public class OtaConfig
{
    /// <summary>命令超时时间（毫秒）</summary>
    public int CommandTimeout { get; set; } = 5000;

    /// <summary>回连超时时间（毫秒）</summary>
    public int ReconnectTimeout { get; set; } = 30000;

    /// <summary>最大重试次数</summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>传输块大小（字节）</summary>
    public int TransferBlockSize { get; set; } = 512;

    /// <summary>发送延迟（毫秒）</summary>
    public int SendDelay { get; set; } = 0;

    /// <summary>是否启用日志</summary>
    public bool EnableLogging { get; set; } = true;
}
