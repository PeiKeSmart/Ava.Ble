namespace JieLi.OTA.Core.Models;

/// <summary>OTA 升级进度</summary>
public class OtaProgress
{
    /// <summary>进度类型</summary>
    public enum ProgressType
    {
        /// <summary>初始化</summary>
        Initialize,
        
        /// <summary>文件传输</summary>
        Transfer,
        
        /// <summary>完成</summary>
        Complete
    }

    /// <summary>类型</summary>
    public ProgressType Type { get; set; }

    /// <summary>当前状态</summary>
    public OtaState State { get; set; }

    /// <summary>状态消息</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>已传输字节数</summary>
    public long TransferredBytes { get; set; }

    /// <summary>总字节数</summary>
    public long TotalBytes { get; set; }

    /// <summary>百分比 (0-100)</summary>
    public int Percentage => TotalBytes > 0 ? (int)(TransferredBytes * 100 / TotalBytes) : 0;

    /// <summary>传输速度 (bytes/s)</summary>
    public long Speed { get; set; }

    /// <summary>剩余时间（秒）</summary>
    public int RemainingSeconds => Speed > 0 ? (int)((TotalBytes - TransferredBytes) / Speed) : 0;

    /// <summary>开始时间</summary>
    public DateTime StartTime { get; set; } = DateTime.Now;

    /// <summary>已用时间</summary>
    public TimeSpan ElapsedTime => DateTime.Now - StartTime;

    public override string ToString()
    {
        return $"{State}: {Message} ({Percentage}%, {Speed / 1024:F2} KB/s)";
    }
}
