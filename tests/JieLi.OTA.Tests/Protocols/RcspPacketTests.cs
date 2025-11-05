using JieLi.OTA.Core.Protocols;

namespace JieLi.OTA.Tests.Protocols;

/// <summary>RcspPacket 单元测试</summary>
public class RcspPacketTests
{
    [Fact(DisplayName = "ToBytes 应生成正确的字节序列")]
    public void ToBytes_ShouldGenerateCorrectFormat()
    {
        // Arrange
        var packet = new RcspPacket
        {
            Flag = 0xC0,
            OpCode = 0x02,
            Payload = [0x01, 0x02, 0x03]
        };
        
        // Act
        var bytes = packet.ToBytes();
        
        // Assert
        Assert.Equal(11, bytes.Length); // 8 + 3
        Assert.Equal(0xFE, bytes[0]); // 帧头1
        Assert.Equal(0xDC, bytes[1]); // 帧头2
        Assert.Equal(0xBA, bytes[2]); // 帧头3
        Assert.Equal(0xC0, bytes[3]); // FLAG
        Assert.Equal(0x02, bytes[4]); // OpCode
        Assert.Equal(0x00, bytes[5]); // Length High
        Assert.Equal(0x03, bytes[6]); // Length Low (3 bytes)
        Assert.Equal(0x01, bytes[7]); // Payload[0]
        Assert.Equal(0x02, bytes[8]); // Payload[1]
        Assert.Equal(0x03, bytes[9]); // Payload[2]
        Assert.Equal(0xEF, bytes[10]); // 帧尾
    }

    [Fact(DisplayName = "ToBytes 无 Payload 应生成最小包")]
    public void ToBytes_WithoutPayload_ShouldGenerateMinimalPacket()
    {
        // Arrange
        var packet = new RcspPacket
        {
            Flag = 0xC0,
            OpCode = 0xE1
        };
        
        // Act
        var bytes = packet.ToBytes();
        
        // Assert
        Assert.Equal(8, bytes.Length);
        Assert.Equal(0xFE, bytes[0]); // 帧头1
        Assert.Equal(0xDC, bytes[1]); // 帧头2
        Assert.Equal(0xBA, bytes[2]); // 帧头3
        Assert.Equal(0xC0, bytes[3]); // FLAG
        Assert.Equal(0xE1, bytes[4]); // OpCode
        Assert.Equal(0x00, bytes[5]); // Length High
        Assert.Equal(0x00, bytes[6]); // Length Low
        Assert.Equal(0xEF, bytes[7]); // 帧尾
    }

    [Fact(DisplayName = "Parse 应正确解析有效数据包")]
    public void Parse_ShouldParseValidPacket()
    {
        // Arrange
        byte[] data = [0xFE, 0xDC, 0xBA, 0xC0, 0x02, 0x00, 0x03, 0x01, 0x02, 0x03, 0xEF];
        
        // Act
        var packet = RcspPacket.Parse(data);
        
        // Assert
        Assert.NotNull(packet);
        Assert.Equal(0xC0, packet.Flag);
        Assert.Equal(0x02, packet.OpCode);
        Assert.Equal(3, packet.Payload.Length);
        Assert.Equal(0x01, packet.Payload[0]);
        Assert.Equal(0x02, packet.Payload[1]);
        Assert.Equal(0x03, packet.Payload[2]);
    }

    [Fact(DisplayName = "Parse 应拒绝过短的数据")]
    public void Parse_ShouldRejectTooShortData()
    {
        // Arrange
        byte[] data = [0xFE, 0xDC, 0xBA, 0xC0];
        
        // Act
        var packet = RcspPacket.Parse(data);
        
        // Assert
        Assert.Null(packet);
    }

    [Fact(DisplayName = "Parse 应拒绝错误的帧头")]
    public void Parse_ShouldRejectInvalidHeader()
    {
        // Arrange
        byte[] data = [0xFE, 0xDC, 0xBB, 0xC0, 0x02, 0x00, 0x00, 0xEF];
        
        // Act
        var packet = RcspPacket.Parse(data);
        
        // Assert
        Assert.Null(packet);
    }

    [Fact(DisplayName = "Parse 应拒绝错误的帧尾")]
    public void Parse_ShouldRejectInvalidTail()
    {
        // Arrange
        byte[] data = [0xFE, 0xDC, 0xBA, 0xC0, 0x02, 0x00, 0x00, 0xEE]; // 错误的帧尾
        
        // Act
        var packet = RcspPacket.Parse(data);
        
        // Assert
        Assert.Null(packet);
    }

    [Fact(DisplayName = "IsCommand 应正确识别命令包")]
    public void IsCommand_ShouldIdentifyCommandPacket()
    {
        // Arrange
        var packet = new RcspPacket { Flag = 0x80 | 0x40 };
        
        // Act & Assert
        Assert.True(packet.IsCommand);
        Assert.True(packet.NeedResponse);
    }

    [Fact(DisplayName = "IsCommand 应正确识别响应包")]
    public void IsCommand_ShouldIdentifyResponsePacket()
    {
        // Arrange
        var packet = new RcspPacket { Flag = 0x00 };
        
        // Act & Assert
        Assert.False(packet.IsCommand);
        Assert.False(packet.NeedResponse);
    }

    [Fact(DisplayName = "ToBytes 和 Parse 应可互逆")]
    public void ToBytes_And_Parse_ShouldBeReversible()
    {
        // Arrange
        var original = new RcspPacket
        {
            Flag = 0xC0,
            OpCode = 0xE5,
            Payload = [0x11, 0x22, 0x33, 0x44, 0x55]
        };
        
        // Act
        var bytes = original.ToBytes();
        var parsed = RcspPacket.Parse(bytes);
        
        // Assert
        Assert.NotNull(parsed);
        Assert.Equal(original.Flag, parsed.Flag);
        Assert.Equal(original.OpCode, parsed.OpCode);
        Assert.Equal(original.Payload.Length, parsed.Payload.Length);
        Assert.Equal(original.Payload, parsed.Payload);
    }
}
