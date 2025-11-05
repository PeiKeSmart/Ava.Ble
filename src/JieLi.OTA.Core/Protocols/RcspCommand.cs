namespace JieLi.OTA.Core.Protocols;

/// <summary>RCSP 命令基类</summary>
public abstract class RcspCommand
{
    /// <summary>操作码</summary>
    public abstract byte OpCode { get; }

    /// <summary>是否需要响应</summary>
    public virtual bool NeedResponse => true;

    /// <summary>序列化命令参数为字节数组（不含 Sn）</summary>
    /// <returns>参数字节数组</returns>
    protected abstract byte[] SerializePayload();

    /// <summary>转换为 RCSP 数据包</summary>
    /// <param name="sn">序列号</param>
    /// <returns>RCSP 数据包</returns>
    public RcspPacket ToPacket(byte sn)
    {
        byte flag = RcspPacket.FLAG_IS_COMMAND;
        if (NeedResponse)
        {
            flag |= RcspPacket.FLAG_NEED_RESPONSE;
        }
        
        // 构建 Payload: [Sn, ...业务数据]
        var businessData = SerializePayload();
        var payload = new byte[1 + businessData.Length];
        payload[0] = sn;  // Sn 作为第一个字节
        if (businessData.Length > 0)
        {
            Buffer.BlockCopy(businessData, 0, payload, 1, businessData.Length);
        }
        
        return new RcspPacket
        {
            Flag = flag,
            OpCode = OpCode,
            Payload = payload
        };
    }
}
