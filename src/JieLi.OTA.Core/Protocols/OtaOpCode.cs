namespace JieLi.OTA.Core.Protocols;

/// <summary>RCSP 操作码定义</summary>
public static class OtaOpCode
{
    /// <summary>获取目标信息（设备信息）</summary>
    public const byte CMD_GET_TARGET_INFO = 0x02;
    
    /// <summary>读取文件偏移</summary>
    public const byte CMD_OTA_READ_FILE_OFFSET = 0xE1;
    
    /// <summary>查询是否可升级</summary>
    public const byte CMD_OTA_INQUIRE_CAN_UPDATE = 0xE2;
    
    /// <summary>进入升级模式</summary>
    public const byte CMD_OTA_ENTER_UPDATE_MODE = 0xE3;
    
    /// <summary>退出升级模式</summary>
    public const byte CMD_OTA_EXIT_UPDATE_MODE = 0xE4;
    
    /// <summary>发送/请求文件块</summary>
    public const byte CMD_OTA_FILE_BLOCK = 0xE5;
    
    /// <summary>查询升级结果</summary>
    public const byte CMD_OTA_QUERY_UPDATE_RESULT = 0xE6;
    
    /// <summary>重启设备/断开连接</summary>
    public const byte CMD_OTA_REBOOT_DEVICE = 0xE7;
    
    /// <summary>通知文件大小</summary>
    public const byte CMD_OTA_NOTIFY_FILE_SIZE = 0xE8;

    /// <summary>切换通信方式 / 设置通信MTU（对应小程序SDK的 CMD_SETTINGS_COMMUNICATION_MTU）</summary>
    public const byte CMD_CHANGE_COMMUNICATION_WAY = 0xD1;
}
