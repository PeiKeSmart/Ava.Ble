namespace JieLi.OTA.Core.Protocols.Responses;

/// <summary>查询升级结果响应</summary>
public class RspUpdateResult : RcspResponse
{
    /// <summary>
    /// 结果代码（业务定义：0=成功，其它值代表仍在处理中/失败等，具体由设备固件定义）
    /// </summary>
    public byte ResultCode { get; set; }

    protected override void ParsePayload(byte[] payload)
    {
        if (payload.Length >= 1)
        {
            ResultCode = payload[0];
        }
    }

    public override string ToString()
    {
        return $"UpdateResult: Status=0x{Status:X2}, Code=0x{ResultCode:X2}";
    }
}
