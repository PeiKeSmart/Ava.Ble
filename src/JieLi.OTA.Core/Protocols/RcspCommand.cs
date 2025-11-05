namespace JieLi.OTA.Core.Protocols;

/// <summary>RCSP 命令基类</summary>
public abstract class RcspCommand
{
    /// <summary>操作码</summary>
    public abstract byte OpCode { get; }

    /// <summary>是否需要响应</summary>
    public virtual bool NeedResponse => true;

    /// <summary>序列化命令参数为字节数组</summary>
    /// <returns>参数字节数组</returns>
    protected abstract byte[] SerializePayload();

    /// <summary>转换为 RCSP 数据包</summary>
    /// <returns>RCSP 数据包</returns>
    public RcspPacket ToPacket()
    {
        byte flag = RcspPacket.FLAG_IS_COMMAND;
        if (NeedResponse)
        {
            flag |= RcspPacket.FLAG_NEED_RESPONSE;
        }
        
        return new RcspPacket
        {
            Flag = flag,
            OpCode = OpCode,
            Payload = SerializePayload()
        };
    }
}
