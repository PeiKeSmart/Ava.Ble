namespace JieLi.OTA.Core.Protocols.Responses;

/// <summary>切换通信方式响应（对应小程序SDK的 ResponseMtu）</summary>
/// <remarks>
/// SDK原始逻辑：
/// - 响应包含设备实际支持的协议MTU或通信方式状态
/// - 对应 ResponseMtu.realProtocolMtu
/// - 根据SDK，如果设备返回错误状态（ERROR_REPLY_BAD_STATUS / ERROR_REPLY_BAD_RESULT），客户端不报错，默认返回0
/// </remarks>
public class RspChangeCommunicationWay : RcspResponse
{
    /// <summary>
    /// 结果代码
    /// - 对应SDK的 realProtocolMtu 或设备返回的状态码
    /// - 0 = 成功或设备不支持（SDK默认返回0）
    /// - 非0 = 其他状态码
    /// </summary>
    public int Result { get; set; }

    protected override void ParsePayload(byte[] payload)
    {
        if (payload.Length >= 2)
        {
            // 小端序或大端序取决于设备协议，这里假设小端（与SDK的 ResponseMtu 一致）
            // SDK: this.realProtocolMtu=(255&t[1])<<8|t[0]
            Result = payload[0] | (payload[1] << 8);
        }
        else if (payload.Length >= 1)
        {
            Result = payload[0];
        }
        else
        {
            Result = 0; // 空响应默认成功
        }
    }

    public override string ToString()
    {
        return $"ChangeCommunicationWay: Status=0x{Status:X2}, Result={Result}";
    }
}
