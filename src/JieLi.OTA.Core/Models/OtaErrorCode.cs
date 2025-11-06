namespace JieLi.OTA.Core.Models;

/// <summary>OTA 错误码（完全对应小程序SDK的错误码定义）</summary>
public static class OtaErrorCode
{
    // ==================== 基础错误码 (-1 ~ -36) ====================
    
    /// <summary>未知错误（对应SDK: ERROR_UNKNOWN）</summary>
    public const int ERROR_UNKNOWN = -1;
    
    /// <summary>成功（对应SDK: ERROR_NONE）</summary>
    public const int SUCCESS = 0;
    public const int ERROR_NONE = 0;
    
    /// <summary>无效参数（对应SDK: ERROR_INVALID_PARAM）</summary>
    public const int ERROR_INVALID_PARAM = -2;
    
    /// <summary>数据格式错误（对应SDK: ERROR_DATA_FORMAT）</summary>
    public const int ERROR_DATA_FORMAT = -3;
    
    /// <summary>未找到资源（对应SDK: ERROR_NOT_FOUND_RESOURCE）</summary>
    public const int ERROR_NOT_FOUND_RESOURCE = -4;
    
    /// <summary>未知设备（对应SDK: ERROR_UNKNOWN_DEVICE）</summary>
    public const int ERROR_UNKNOWN_DEVICE = -32;
    
    /// <summary>设备离线（对应SDK: ERROR_DEVICE_OFFLINE）</summary>
    public const int ERROR_DEVICE_OFFLINE = -33;
    
    /// <summary>IO异常（对应SDK: ERROR_IO_EXCEPTION）</summary>
    public const int ERROR_IO_EXCEPTION = -35;
    
    /// <summary>重复状态（对应SDK: ERROR_REPEAT_STATUS）</summary>
    public const int ERROR_REPEAT_STATUS = -36;
    
    // ==================== 协议错误码 (-64 ~ -67) ====================
    
    /// <summary>等待响应超时（对应SDK: ERROR_RESPONSE_TIMEOUT）</summary>
    public const int ERROR_RESPONSE_TIMEOUT = -64;
    
    /// <summary>设备返回错误状态（对应SDK: ERROR_REPLY_BAD_STATUS）</summary>
    public const int ERROR_REPLY_BAD_STATUS = -65;
    
    /// <summary>设备返回错误结果（对应SDK: ERROR_REPLY_BAD_RESULT）</summary>
    public const int ERROR_REPLY_BAD_RESULT = -66;
    
    /// <summary>没有关联的解析器（对应SDK: ERROR_NONE_PARSER）</summary>
    public const int ERROR_NONE_PARSER = -67;
    
    // ==================== OTA特定错误码 (-97 ~ -114) ====================
    
    /// <summary>设备电量低（对应SDK: ERROR_OTA_LOW_POWER）</summary>
    public const int ERROR_LOW_POWER = -97;
    public const int ERROR_OTA_LOW_POWER = -97;
    
    /// <summary>升级固件信息错误（对应SDK: ERROR_OTA_UPDATE_FILE）</summary>
    public const int ERROR_OTA_UPDATE_FILE = -98;
    
    /// <summary>固件版本未变化（对应SDK: ERROR_OTA_FIRMWARE_VERSION_NO_CHANGE）</summary>
    public const int ERROR_VERSION_NO_CHANGE = -99;
    public const int ERROR_OTA_FIRMWARE_VERSION_NO_CHANGE = -99;
    
    /// <summary>TWS未连接（对应SDK: ERROR_OTA_TWS_NOT_CONNECT）</summary>
    public const int ERROR_TWS_NOT_CONNECT = -100;
    public const int ERROR_OTA_TWS_NOT_CONNECT = -100;
    
    /// <summary>耳机不在充电仓（对应SDK: ERROR_OTA_HEADSET_NOT_IN_CHARGING_BIN）</summary>
    public const int ERROR_OTA_HEADSET_NOT_IN_CHARGING_BIN = -101;
    
    /// <summary>数据校验错误（对应SDK: ERROR_OTA_DATA_CHECK_ERROR）</summary>
    public const int ERROR_DATA_CHECK = -102;
    public const int ERROR_OTA_DATA_CHECK_ERROR = -102;
    
    /// <summary>升级失败（对应SDK: ERROR_OTA_FAIL）</summary>
    public const int ERROR_OTA_FAIL = -103;
    
    /// <summary>加密密钥不匹配（对应SDK: ERROR_OTA_ENCRYPTED_KEY_NOT_MATCH）</summary>
    public const int ERROR_ENCRYPTED_KEY_NOT_MATCH = -104;
    public const int ERROR_OTA_ENCRYPTED_KEY_NOT_MATCH = -104;
    
    /// <summary>升级文件损坏（对应SDK: ERROR_OTA_UPGRADE_FILE_ERROR）</summary>
    public const int ERROR_OTA_UPGRADE_FILE_ERROR = -105;
    
    /// <summary>升级类型错误（对应SDK: ERROR_OTA_UPGRADE_TYPE_ERROR）</summary>
    public const int ERROR_OTA_UPGRADE_TYPE_ERROR = -106;
    
    /// <summary>升级时长度错误（对应SDK: ERROR_OTA_LENGTH_OVER）</summary>
    public const int ERROR_OTA_LENGTH_OVER = -107;
    
    /// <summary>Flash读写错误（对应SDK: ERROR_OTA_FLASH_IO_EXCEPTION）</summary>
    public const int ERROR_OTA_FLASH_IO_EXCEPTION = -108;
    
    /// <summary>设备等待命令超时（对应SDK: ERROR_OTA_CMD_TIMEOUT）</summary>
    public const int ERROR_OTA_CMD_TIMEOUT = -109;
    
    /// <summary>OTA正在进行中（对应SDK: ERROR_OTA_IN_PROGRESS）</summary>
    public const int ERROR_OTA_IN_PROGRESS = -110;
    
    /// <summary>SDK等待命令超时（对应SDK: ERROR_OTA_COMMAND_TIMEOUT）</summary>
    public const int ERROR_COMMAND_TIMEOUT = -111;
    public const int ERROR_OTA_COMMAND_TIMEOUT = -111;
    
    /// <summary>等待重连设备超时（对应SDK: ERROR_OTA_RECONNECT_DEVICE_TIMEOUT）</summary>
    public const int ERROR_RECONNECT_TIMEOUT = -112;
    public const int ERROR_OTA_RECONNECT_DEVICE_TIMEOUT = -112;
    
    /// <summary>取消升级（对应SDK: ERROR_OTA_USE_CANCEL）</summary>
    public const int ERROR_OTA_USE_CANCEL = -113;
    
    /// <summary>相同的升级文件（对应SDK: ERROR_OTA_SAME_FILE）</summary>
    public const int ERROR_OTA_SAME_FILE = -114;
    
    // ==================== C#自定义扩展错误码 (-200+) ====================
    
    /// <summary>用户取消（C#扩展）</summary>
    public const int ERROR_USER_CANCELLED = -200;
    
    /// <summary>连接断开（C#扩展）</summary>
    public const int ERROR_CONNECTION_LOST = -201;

    /// <summary>获取错误描述</summary>
    /// <param name="errorCode">错误码</param>
    /// <returns>错误描述</returns>
    public static string GetErrorDescription(int errorCode)
    {
        return errorCode switch
        {
            // 基础错误
            ERROR_UNKNOWN => "未知错误",
            SUCCESS => "成功",
            ERROR_INVALID_PARAM => "无效参数",
            ERROR_DATA_FORMAT => "数据格式错误",
            ERROR_NOT_FOUND_RESOURCE => "未找到资源",
            ERROR_UNKNOWN_DEVICE => "未知设备",
            ERROR_DEVICE_OFFLINE => "设备离线",
            ERROR_IO_EXCEPTION => "IO异常",
            ERROR_REPEAT_STATUS => "重复状态",
            
            // 协议错误
            ERROR_RESPONSE_TIMEOUT => "等待响应超时",
            ERROR_REPLY_BAD_STATUS => "设备返回错误状态",
            ERROR_REPLY_BAD_RESULT => "设备返回错误结果",
            ERROR_NONE_PARSER => "没有关联的解析器",
            
            // OTA特定错误
            ERROR_OTA_LOW_POWER => "设备电量过低",
            ERROR_OTA_UPDATE_FILE => "升级固件信息错误",
            ERROR_OTA_FIRMWARE_VERSION_NO_CHANGE => "固件版本未变化",
            ERROR_OTA_TWS_NOT_CONNECT => "TWS未连接",
            ERROR_OTA_HEADSET_NOT_IN_CHARGING_BIN => "耳机不在充电仓",
            ERROR_OTA_DATA_CHECK_ERROR => "数据校验错误",
            ERROR_OTA_FAIL => "升级失败",
            ERROR_OTA_ENCRYPTED_KEY_NOT_MATCH => "加密密钥不匹配",
            ERROR_OTA_UPGRADE_FILE_ERROR => "升级文件损坏",
            ERROR_OTA_UPGRADE_TYPE_ERROR => "升级类型错误",
            ERROR_OTA_LENGTH_OVER => "升级时长度错误",
            ERROR_OTA_FLASH_IO_EXCEPTION => "Flash读写错误",
            ERROR_OTA_CMD_TIMEOUT => "设备等待命令超时",
            ERROR_OTA_IN_PROGRESS => "OTA正在进行中",
            ERROR_OTA_COMMAND_TIMEOUT => "SDK等待命令超时",
            ERROR_OTA_RECONNECT_DEVICE_TIMEOUT => "等待重连设备超时",
            ERROR_OTA_USE_CANCEL => "取消升级",
            ERROR_OTA_SAME_FILE => "相同的升级文件",
            
            // C#扩展错误
            ERROR_USER_CANCELLED => "用户取消升级",
            ERROR_CONNECTION_LOST => "连接断开",
            
            _ => $"未知错误: {errorCode}"
        };
    }
}
