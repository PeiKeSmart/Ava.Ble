using JieLi.OTA.Core.Protocols;
using Xunit;

namespace JieLi.OTA.Tests.Protocols;

public class SimpleProtocolTest
{
    [Fact(DisplayName = "验证新协议格式的序列化和反序列化")]
    public void NewProtocol_SerializeAndParse_ShouldWork()
    {
        // Arrange - 创建一个命令包
        var commandPacket = new RcspPacket
        {
            Flag = 0xC0, // 命令包(0x80 | 0x40)
            OpCode = 0x02,
            Payload = []
        };

        // Act - 序列化
        var commandBytes = commandPacket.ToBytes();
        
        // Assert - 验证格式
        Assert.Equal(0xFE, commandBytes[0]); // 帧头1
        Assert.Equal(0xDC, commandBytes[1]); // 帧头2
        Assert.Equal(0xBA, commandBytes[2]); // 帧头3
        Assert.Equal(0xC0, commandBytes[3]); // Flag
        Assert.Equal(0x02, commandBytes[4]); // OpCode
        Assert.Equal(0x00, commandBytes[5]); // Len高字节
        Assert.Equal(0x00, commandBytes[6]); // Len低字节
        Assert.Equal(0xEF, commandBytes[7]); // 帧尾
        
        // Arrange - 创建一个响应包
        var responsePacket = new RcspPacket
        {
            Flag = 0x01, // 响应包
            OpCode = 0x02,
            Payload = [0x11, 0x22, 0x33]
        };
        
        // Act - 序列化
        var responseBytes = responsePacket.ToBytes();
        
        // Assert - 验证格式
        Assert.Equal(0xFE, responseBytes[0]); // 帧头1
        Assert.Equal(0xDC, responseBytes[1]); // 帧头2
        Assert.Equal(0xBA, responseBytes[2]); // 帧头3
        Assert.Equal(0x01, responseBytes[3]); // Flag
        Assert.Equal(0x02, responseBytes[4]); // OpCode
        Assert.Equal(0x00, responseBytes[5]); // Len高字节(长度=3)
        Assert.Equal(0x03, responseBytes[6]); // Len低字节
        Assert.Equal(0x11, responseBytes[7]); // Payload[0]
        Assert.Equal(0x22, responseBytes[8]); // Payload[1]
        Assert.Equal(0x33, responseBytes[9]); // Payload[2]
        Assert.Equal(0xEF, responseBytes[10]); // 帧尾
        
        // Act - 解析响应包
        var parsed = RcspPacket.Parse(responseBytes);
        
        // Assert - 验证解析结果
        Assert.NotNull(parsed);
        Assert.Equal(0x01, parsed.Flag);
        Assert.Equal(0x02, parsed.OpCode);
        Assert.False(parsed.IsCommand); // 响应包
        Assert.Equal(3, parsed.Payload.Length);
        Assert.Equal(0x11, parsed.Payload[0]);
        Assert.Equal(0x22, parsed.Payload[1]);
        Assert.Equal(0x33, parsed.Payload[2]);
    }
}
