namespace JieLi.OTA.Core.Protocols.Responses;

/// <summary>查询可升级响应</summary>
public class RspCanUpdate : RcspResponse
{
    /// <summary>可以升级</summary>
    public const byte RESULT_CAN_UPDATE = 0x00;
    
    /// <summary>设备电量低</summary>
    public const byte RESULT_LOW_POWER = 0x01;
    
    /// <summary>固件信息错误</summary>
    public const byte RESULT_FIRMWARE_INFO_ERROR = 0x02;
    
    /// <summary>版本一致</summary>
    public const byte RESULT_VERSION_SAME = 0x03;
    
    /// <summary>TWS 未连接</summary>
    public const byte RESULT_TWS_NOT_CONNECT = 0x04;
    
    /// <summary>耳机不在充电仓</summary>
    public const byte RESULT_NOT_IN_CHARGING_BOX = 0x05;

    /// <summary>查询结果</summary>
    public byte Result { get; set; }

    /// <summary>是否可以升级</summary>
    public bool CanUpdate => Result == RESULT_CAN_UPDATE;

    protected override void ParsePayload(byte[] payload)
    {
        if (payload.Length > 0)
        {
            Result = payload[0];
        }
    }

    public override string ToString()
    {
        var message = Result switch
        {
            RESULT_CAN_UPDATE => "可以升级",
            RESULT_LOW_POWER => "设备电量低",
            RESULT_FIRMWARE_INFO_ERROR => "固件信息错误",
            RESULT_VERSION_SAME => "版本一致",
            RESULT_TWS_NOT_CONNECT => "TWS 未连接",
            RESULT_NOT_IN_CHARGING_BOX => "耳机不在充电仓",
            _ => $"未知结果: {Result:X2}"
        };
        
        return $"CanUpdate: {CanUpdate}, Message: {message}";
    }
}
