namespace JieLi.OTA.Core.Protocols;

/// <summary>RCSP 数据包</summary>
/// <remarks>
/// 数据包格式: AA 55 [FLAG] [SN] [OpCode] [Payload...] AD
/// - AA 55: 固定帧头
/// - FLAG: 标志位 (bit7=是否为命令, bit6=是否需要响应)
/// - SN: 序列号，用于匹配命令与响应
/// - OpCode: 操作码
/// - Payload: 数据负载（可选）
/// - AD: 固定帧尾
/// </remarks>
public class RcspPacket
{
    /// <summary>帧头字节1</summary>
    public const byte RCSP_HEAD_1 = 0xAA;
    
    /// <summary>帧头字节2</summary>
    public const byte RCSP_HEAD_2 = 0x55;
    
    /// <summary>帧尾字节</summary>
    public const byte RCSP_END = 0xAD;
    
    /// <summary>最小数据包长度（AA 55 FLAG SN OpCode AD）</summary>
    public const int MIN_PACKET_LENGTH = 6;
    
    /// <summary>FLAG: 是否为命令（bit7）</summary>
    public const byte FLAG_IS_COMMAND = 0x80;
    
    /// <summary>FLAG: 是否需要响应（bit6）</summary>
    public const byte FLAG_NEED_RESPONSE = 0x40;

    /// <summary>标志位</summary>
    public byte Flag { get; set; }

    /// <summary>序列号</summary>
    public byte Sn { get; set; }

    /// <summary>操作码</summary>
    public byte OpCode { get; set; }

    /// <summary>数据负载</summary>
    public byte[] Payload { get; set; } = [];

    /// <summary>是否为命令</summary>
    public bool IsCommand => (Flag & FLAG_IS_COMMAND) != 0;

    /// <summary>是否需要响应</summary>
    public bool NeedResponse => (Flag & FLAG_NEED_RESPONSE) != 0;

    /// <summary>将数据包序列化为字节数组</summary>
    /// <returns>字节数组</returns>
    public byte[] ToBytes()
    {
        var length = MIN_PACKET_LENGTH + Payload.Length;
        var buffer = new byte[length];
        
        buffer[0] = RCSP_HEAD_1;
        buffer[1] = RCSP_HEAD_2;
        buffer[2] = Flag;
        buffer[3] = Sn;
        buffer[4] = OpCode;
        
        if (Payload.Length > 0)
        {
            Buffer.BlockCopy(Payload, 0, buffer, 5, Payload.Length);
        }
        
        buffer[^1] = RCSP_END;
        
        return buffer;
    }

    /// <summary>从字节数组解析数据包</summary>
    /// <param name="data">字节数组</param>
    /// <returns>解析后的数据包，失败返回 null</returns>
    public static RcspPacket? Parse(byte[] data)
    {
        if (data.Length < MIN_PACKET_LENGTH)
            return null;
        
        // 校验帧头
        if (data[0] != RCSP_HEAD_1 || data[1] != RCSP_HEAD_2)
            return null;
        
        // 校验帧尾
        if (data[^1] != RCSP_END)
            return null;
        
        var packet = new RcspPacket
        {
            Flag = data[2],
            Sn = data[3],
            OpCode = data[4]
        };
        
        // 提取 Payload
        var payloadLength = data.Length - MIN_PACKET_LENGTH;
        if (payloadLength > 0)
        {
            packet.Payload = new byte[payloadLength];
            Buffer.BlockCopy(data, 5, packet.Payload, 0, payloadLength);
        }
        
        return packet;
    }

    /// <summary>转换为十六进制字符串（用于调试）</summary>
    public override string ToString()
    {
        var bytes = ToBytes();
        return $"[{bytes.Length}] {BitConverter.ToString(bytes).Replace("-", " ")}";
    }
}
