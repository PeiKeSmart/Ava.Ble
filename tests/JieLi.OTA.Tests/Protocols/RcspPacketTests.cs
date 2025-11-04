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
            Sn = 1,
            OpCode = 0x02,
            Payload = [0x01, 0x02, 0x03]
        };
        
        // Act
        var bytes = packet.ToBytes();
        
        // Assert
        Assert.Equal(9, bytes.Length); // 6 + 3
        Assert.Equal(0xAA, bytes[0]); // 帧头1
        Assert.Equal(0x55, bytes[1]); // 帧头2
        Assert.Equal(0xC0, bytes[2]); // FLAG
        Assert.Equal(1, bytes[3]);    // SN
        Assert.Equal(0x02, bytes[4]); // OpCode
        Assert.Equal(0x01, bytes[5]); // Payload[0]
        Assert.Equal(0x02, bytes[6]); // Payload[1]
        Assert.Equal(0x03, bytes[7]); // Payload[2]
        Assert.Equal(0xAD, bytes[8]); // 帧尾
    }

    [Fact(DisplayName = "ToBytes 无 Payload 应生成最小包")]
    public void ToBytes_WithoutPayload_ShouldGenerateMinimalPacket()
    {
        // Arrange
        var packet = new RcspPacket
        {
            Flag = 0xC0,
            Sn = 5,
            OpCode = 0xE0
        };
        
        // Act
        var bytes = packet.ToBytes();
        
        // Assert
        Assert.Equal(6, bytes.Length);
        Assert.Equal(0xAA, bytes[0]);
        Assert.Equal(0x55, bytes[1]);
        Assert.Equal(0xC0, bytes[2]);
        Assert.Equal(5, bytes[3]);
        Assert.Equal(0xE0, bytes[4]);
        Assert.Equal(0xAD, bytes[5]);
    }

    [Fact(DisplayName = "Parse 应正确解析有效数据包")]
    public void Parse_ShouldParseValidPacket()
    {
        // Arrange
        byte[] data = [0xAA, 0x55, 0xC0, 0x01, 0x02, 0x01, 0x02, 0x03, 0xAD];
        
        // Act
        var packet = RcspPacket.Parse(data);
        
        // Assert
        Assert.NotNull(packet);
        Assert.Equal(0xC0, packet.Flag);
        Assert.Equal(1, packet.Sn);
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
        byte[] data = [0xAA, 0x55, 0xC0];
        
        // Act
        var packet = RcspPacket.Parse(data);
        
        // Assert
        Assert.Null(packet);
    }

    [Fact(DisplayName = "Parse 应拒绝错误的帧头")]
    public void Parse_ShouldRejectInvalidHeader()
    {
        // Arrange
        byte[] data = [0xAB, 0x55, 0xC0, 0x01, 0x02, 0xAD];
        
        // Act
        var packet = RcspPacket.Parse(data);
        
        // Assert
        Assert.Null(packet);
    }

    [Fact(DisplayName = "Parse 应拒绝错误的帧尾")]
    public void Parse_ShouldRejectInvalidTail()
    {
        // Arrange
        byte[] data = [0xAA, 0x55, 0xC0, 0x01, 0x02, 0xAE];
        
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
            Sn = 123,
            OpCode = 0xE5,
            Payload = [0x11, 0x22, 0x33, 0x44, 0x55]
        };
        
        // Act
        var bytes = original.ToBytes();
        var parsed = RcspPacket.Parse(bytes);
        
        // Assert
        Assert.NotNull(parsed);
        Assert.Equal(original.Flag, parsed.Flag);
        Assert.Equal(original.Sn, parsed.Sn);
        Assert.Equal(original.OpCode, parsed.OpCode);
        Assert.Equal(original.Payload, parsed.Payload);
    }
}
