namespace JieLi.OTA.Core.Protocols;

/// <summary>RCSP 响应基类</summary>
public abstract class RcspResponse
{
    /// <summary>操作码</summary>
    public byte OpCode { get; set; }

    /// <summary>序列号</summary>
    public byte Sn { get; set; }

    /// <summary>原始 Payload 数据</summary>
    public byte[] RawPayload { get; set; } = [];

    /// <summary>从数据包创建响应对象</summary>
    /// <param name="packet">RCSP 数据包</param>
    public virtual void FromPacket(RcspPacket packet)
    {
        OpCode = packet.OpCode;
        Sn = packet.Sn;
        RawPayload = packet.Payload;
        
        if (RawPayload.Length > 0)
        {
            ParsePayload(RawPayload);
        }
    }

    /// <summary>解析 Payload 数据</summary>
    /// <param name="payload">Payload 字节数组</param>
    protected abstract void ParsePayload(byte[] payload);
}
