namespace JieLi.OTA.Core.Models;

/// <summary>OTA 状态</summary>
public enum OtaState
{
    /// <summary>空闲</summary>
    Idle,
    
    /// <summary>连接设备</summary>
    Connecting,
    
    /// <summary>获取设备信息</summary>
    GettingDeviceInfo,
    
    /// <summary>读取文件偏移</summary>
    ReadingFileOffset,
    
    /// <summary>验证固件</summary>
    ValidatingFirmware,
    
    /// <summary>进入升级模式</summary>
    EnteringUpdateMode,
    
    /// <summary>等待回连（单备份）</summary>
    WaitingReconnect,
    
    /// <summary>传输文件</summary>
    TransferringFile,
    
    /// <summary>查询升级结果</summary>
    QueryingResult,
    
    /// <summary>重启设备</summary>
    Rebooting,
    
    /// <summary>完成</summary>
    Completed,
    
    /// <summary>失败</summary>
    Failed,
    
    /// <summary>已取消</summary>
    Cancelled
}
