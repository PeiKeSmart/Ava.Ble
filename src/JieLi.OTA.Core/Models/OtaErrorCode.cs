namespace JieLi.OTA.Core.Models;

/// <summary>OTA 错误码</summary>
public static class OtaErrorCode
{
    /// <summary>成功</summary>
    public const int SUCCESS = 0;
    
    /// <summary>设备电量低</summary>
    public const int ERROR_LOW_POWER = -97;
    
    /// <summary>固件版本未变化</summary>
    public const int ERROR_VERSION_NO_CHANGE = -99;
    
    /// <summary>TWS 未连接</summary>
    public const int ERROR_TWS_NOT_CONNECT = -100;
    
    /// <summary>数据校验错误</summary>
    public const int ERROR_DATA_CHECK = -102;
    
    /// <summary>升级失败</summary>
    public const int ERROR_OTA_FAIL = -103;
    
    /// <summary>加密密钥不匹配</summary>
    public const int ERROR_ENCRYPTED_KEY_NOT_MATCH = -104;
    
    /// <summary>命令超时</summary>
    public const int ERROR_COMMAND_TIMEOUT = -111;
    
    /// <summary>回连超时</summary>
    public const int ERROR_RECONNECT_TIMEOUT = -112;
    
    /// <summary>用户取消</summary>
    public const int ERROR_USER_CANCELLED = -200;
    
    /// <summary>连接断开</summary>
    public const int ERROR_CONNECTION_LOST = -201;

    /// <summary>获取错误描述</summary>
    /// <param name="errorCode">错误码</param>
    /// <returns>错误描述</returns>
    public static string GetErrorDescription(int errorCode)
    {
        return errorCode switch
        {
            SUCCESS => "成功",
            ERROR_LOW_POWER => "设备电量过低",
            ERROR_VERSION_NO_CHANGE => "固件版本未变化",
            ERROR_TWS_NOT_CONNECT => "TWS 未连接",
            ERROR_DATA_CHECK => "数据校验错误",
            ERROR_OTA_FAIL => "升级失败",
            ERROR_ENCRYPTED_KEY_NOT_MATCH => "加密密钥不匹配",
            ERROR_COMMAND_TIMEOUT => "命令超时",
            ERROR_RECONNECT_TIMEOUT => "回连设备超时",
            ERROR_USER_CANCELLED => "用户取消升级",
            ERROR_CONNECTION_LOST => "连接断开",
            _ => $"未知错误: {errorCode}"
        };
    }
}
