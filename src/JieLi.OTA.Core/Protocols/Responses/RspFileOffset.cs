namespace JieLi.OTA.Core.Protocols.Responses;

/// <summary>文件偏移响应</summary>
public class RspFileOffset : RcspResponse
{
    /// <summary>文件偏移量</summary>
    public uint Offset { get; set; }

    protected override void ParsePayload(byte[] payload)
    {
        if (payload.Length >= 4)
        {
            // 小端序
            Offset = BitConverter.ToUInt32(payload, 0);
        }
    }

    public override string ToString()
    {
        return $"FileOffset: {Offset} bytes";
    }
}
