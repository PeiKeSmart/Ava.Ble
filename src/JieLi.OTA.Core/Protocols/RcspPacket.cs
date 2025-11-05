namespace JieLi.OTA.Core.Protocols;

/// <summary>RCSP 数据包</summary>
/// <remarks>
/// 数据包格式: FE DC BA [FLAG] [OpCode] [LEN_H] [LEN_L] [Payload...] EF
/// - FE DC BA: 固定帧头 (3字节)
/// - FLAG: 标志位 (bit7=是否为命令, bit6=是否需要响应)
/// - OpCode: 操作码
/// - LEN: Payload 长度 (2字节, 大端序)
/// - Payload: 数据负载
///   * Command: [Sn, ...业务参数]
///   * Response: [Status, Sn, ...业务数据]
/// - EF: 固定帧尾
/// </remarks>
public class RcspPacket
{
    /// <summary>帧头字节1</summary>
    public const byte RCSP_HEAD_1 = 0xFE;
    
    /// <summary>帧头字节2</summary>
    public const byte RCSP_HEAD_2 = 0xDC;
    
    /// <summary>帧头字节3</summary>
    public const byte RCSP_HEAD_3 = 0xBA;
    
    /// <summary>帧尾字节</summary>
    public const byte RCSP_END = 0xEF;
    
    /// <summary>最小数据包长度（FE DC BA FLAG OpCode LEN_H LEN_L EF）</summary>
    public const int MIN_PACKET_LENGTH = 8;
    
    /// <summary>FLAG: 是否为命令（bit7）</summary>
    public const byte FLAG_IS_COMMAND = 0x80;
    
    /// <summary>FLAG: 是否需要响应（bit6）</summary>
    public const byte FLAG_NEED_RESPONSE = 0x40;

    /// <summary>标志位</summary>
    public byte Flag { get; set; }

    /// <summary>操作码</summary>
    public byte OpCode { get; set; }

    /// <summary>数据负载（Command: [Sn, ...], Response: [Status, Sn, ...]）</summary>
    public byte[] Payload { get; set; } = [];

    /// <summary>是否为命令</summary>
    public bool IsCommand => (Flag & FLAG_IS_COMMAND) != 0;

    /// <summary>是否需要响应</summary>
    public bool NeedResponse => (Flag & FLAG_NEED_RESPONSE) != 0;

    /// <summary>将数据包序列化为字节数组</summary>
    /// <returns>字节数组</returns>
    public byte[] ToBytes()
    {
        var payloadLen = Payload.Length;
        var length = MIN_PACKET_LENGTH + payloadLen;
        var buffer = new byte[length];
        
        // 帧头 (3字节)
        buffer[0] = RCSP_HEAD_1;
        buffer[1] = RCSP_HEAD_2;
        buffer[2] = RCSP_HEAD_3;
        
        // FLAG
        buffer[3] = Flag;
        
        // OpCode
        buffer[4] = OpCode;
        
        // Payload长度 (2字节, 大端序)
        buffer[5] = (byte)(payloadLen >> 8);
        buffer[6] = (byte)(payloadLen & 0xFF);
        
        // Payload
        if (payloadLen > 0)
        {
            Buffer.BlockCopy(Payload, 0, buffer, 7, payloadLen);
        }
        
        // 帧尾
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
        
        // 校验帧头 (3字节)
        if (data[0] != RCSP_HEAD_1 || data[1] != RCSP_HEAD_2 || data[2] != RCSP_HEAD_3)
            return null;
        
        // 校验帧尾
        if (data[^1] != RCSP_END)
            return null;
        
        var packet = new RcspPacket
        {
            Flag = data[3],
            OpCode = data[4]
        };
        
        // 读取 Payload 长度 (大端序)
        var payloadLength = (data[5] << 8) | data[6];
        
        // 校验长度
        if (data.Length != MIN_PACKET_LENGTH + payloadLength)
            return null;
        
        // 提取 Payload
        if (payloadLength > 0)
        {
            packet.Payload = new byte[payloadLength];
            Buffer.BlockCopy(data, 7, packet.Payload, 0, payloadLength);
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
