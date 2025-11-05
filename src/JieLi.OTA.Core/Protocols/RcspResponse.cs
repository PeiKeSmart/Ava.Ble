namespace JieLi.OTA.Core.Protocols;

/// <summary>RCSP 响应基类</summary>
public abstract class RcspResponse
{
    /// <summary>操作码</summary>
    public byte OpCode { get; set; }

    /// <summary>状态码</summary>
    public byte Status { get; set; }

    /// <summary>序列号</summary>
    public byte Sn { get; set; }

    /// <summary>原始 Payload 数据（不含 Status 和 Sn）</summary>
    public byte[] RawPayload { get; set; } = [];

    /// <summary>从数据包创建响应对象</summary>
    /// <param name="packet">RCSP 数据包</param>
    public virtual void FromPacket(RcspPacket packet)
    {
        OpCode = packet.OpCode;
        
        // Response Payload 格式: [Status, Sn, ...业务数据]
        if (packet.Payload.Length < 2)
        {
            throw new InvalidDataException("Response Payload 长度不足，至少需要 2 字节 (Status + Sn)");
        }
        
        Status = packet.Payload[0];
        Sn = packet.Payload[1];
        
        // 提取业务数据（从 index 2 开始）
        if (packet.Payload.Length > 2)
        {
            RawPayload = new byte[packet.Payload.Length - 2];
            Buffer.BlockCopy(packet.Payload, 2, RawPayload, 0, RawPayload.Length);
            ParsePayload(RawPayload);
        }
    }

    /// <summary>解析业务 Payload 数据（不含 Status 和 Sn）</summary>
    /// <param name="payload">业务 Payload 字节数组</param>
    protected abstract void ParsePayload(byte[] payload);
}
