namespace JieLi.OTA.Core.Protocols.Commands;

/// <summary>获取目标设备信息命令</summary>
public class CmdGetTargetInfo : RcspCommand
{
    /// <summary>Mask 参数（默认 0xFFFFFFFF 表示获取所有信息）</summary>
    public uint Mask { get; set; } = 0xFFFFFFFF;

    /// <summary>平台类型（默认 0x00）</summary>
    public byte Platform { get; set; } = 0x00;

    public override byte OpCode => OtaOpCode.CMD_GET_TARGET_INFO;

    protected override byte[] SerializePayload()
    {
        var payload = new byte[5];
        
        // Mask (4字节，小端序)
        payload[0] = (byte)(Mask & 0xFF);
        payload[1] = (byte)((Mask >> 8) & 0xFF);
        payload[2] = (byte)((Mask >> 16) & 0xFF);
        payload[3] = (byte)((Mask >> 24) & 0xFF);
        
        // Platform (1字节)
        payload[4] = Platform;
        
        return payload;
    }
}
