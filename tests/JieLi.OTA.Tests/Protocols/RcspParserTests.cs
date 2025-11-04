using JieLi.OTA.Core.Protocols;

namespace JieLi.OTA.Tests.Protocols;

/// <summary>RcspParser 单元测试</summary>
public class RcspParserTests
{
    [Fact(DisplayName = "TryParse 应能从完整数据解析出包")]
    public void TryParse_ShouldParseCompletePacket()
    {
        // Arrange
        var parser = new RcspParser();
        byte[] data = [0xAA, 0x55, 0xC0, 0x01, 0x02, 0xAD];
        
        // Act
        parser.AddData(data);
        var packet = parser.TryParse();
        
        // Assert
        Assert.NotNull(packet);
        Assert.Equal(0xC0, packet.Flag);
        Assert.Equal(1, packet.Sn);
        Assert.Equal(0x02, packet.OpCode);
    }

    [Fact(DisplayName = "TryParse 应处理分片数据")]
    public void TryParse_ShouldHandleFragmentedData()
    {
        // Arrange
        var parser = new RcspParser();
        byte[] part1 = [0xAA, 0x55, 0xC0];
        byte[] part2 = [0x01, 0x02];
        byte[] part3 = [0xAD];
        
        // Act
        parser.AddData(part1);
        var packet1 = parser.TryParse();
        Assert.Null(packet1); // 数据不完整
        
        parser.AddData(part2);
        var packet2 = parser.TryParse();
        Assert.Null(packet2); // 仍然不完整
        
        parser.AddData(part3);
        var packet3 = parser.TryParse();
        
        // Assert
        Assert.NotNull(packet3);
        Assert.Equal(0x01, packet3.Sn);
    }

    [Fact(DisplayName = "TryParse 应能解析多个连续的包")]
    public void TryParse_ShouldParseMultiplePackets()
    {
        // Arrange
        var parser = new RcspParser();
        byte[] data = [
            0xAA, 0x55, 0xC0, 0x01, 0x02, 0xAD,  // 包1
            0xAA, 0x55, 0x40, 0x02, 0xE0, 0xAD   // 包2
        ];
        
        // Act
        parser.AddData(data);
        
        var packet1 = parser.TryParse();
        var packet2 = parser.TryParse();
        var packet3 = parser.TryParse();
        
        // Assert
        Assert.NotNull(packet1);
        Assert.Equal(1, packet1.Sn);
        
        Assert.NotNull(packet2);
        Assert.Equal(2, packet2.Sn);
        
        Assert.Null(packet3); // 没有更多包
    }

    [Fact(DisplayName = "TryParse 应丢弃帧头前的无效数据")]
    public void TryParse_ShouldDiscardDataBeforeHeader()
    {
        // Arrange
        var parser = new RcspParser();
        byte[] data = [
            0x11, 0x22, 0x33,                    // 无效数据
            0xAA, 0x55, 0xC0, 0x01, 0x02, 0xAD   // 有效包
        ];
        
        // Act
        parser.AddData(data);
        var packet = parser.TryParse();
        
        // Assert
        Assert.NotNull(packet);
        Assert.Equal(1, packet.Sn);
    }

    [Fact(DisplayName = "TryParse 未找到帧尾应等待更多数据")]
    public void TryParse_ShouldWaitForTail()
    {
        // Arrange
        var parser = new RcspParser();
        byte[] data = [0xAA, 0x55, 0xC0, 0x01, 0x02]; // 缺少帧尾
        
        // Act
        parser.AddData(data);
        var packet1 = parser.TryParse();
        
        // 补充帧尾
        parser.AddData([0xAD]);
        var packet2 = parser.TryParse();
        
        // Assert
        Assert.Null(packet1);
        Assert.NotNull(packet2);
    }

    [Fact(DisplayName = "Clear 应清空缓冲区")]
    public void Clear_ShouldEmptyBuffer()
    {
        // Arrange
        var parser = new RcspParser();
        parser.AddData([0xAA, 0x55, 0xC0]);
        Assert.True(parser.BufferSize > 0);
        
        // Act
        parser.Clear();
        
        // Assert
        Assert.Equal(0, parser.BufferSize);
    }

    [Fact(DisplayName = "AddData 应支持 ReadOnlySpan")]
    public void AddData_ShouldSupportReadOnlySpan()
    {
        // Arrange
        var parser = new RcspParser();
        ReadOnlySpan<byte> data = stackalloc byte[] { 0xAA, 0x55, 0xC0, 0x01, 0x02, 0xAD };
        
        // Act
        parser.AddData(data);
        var packet = parser.TryParse();
        
        // Assert
        Assert.NotNull(packet);
        Assert.Equal(1, packet.Sn);
    }

    [Fact(DisplayName = "BufferSize 应正确返回缓冲区大小")]
    public void BufferSize_ShouldReturnCorrectSize()
    {
        // Arrange
        var parser = new RcspParser();
        
        // Act & Assert
        Assert.Equal(0, parser.BufferSize);
        
        parser.AddData([0xAA, 0x55, 0xC0]);
        Assert.Equal(3, parser.BufferSize);
        
        parser.AddData([0x01, 0x02, 0xAD]);
        Assert.Equal(6, parser.BufferSize);
        
        parser.TryParse();
        Assert.Equal(0, parser.BufferSize); // 解析后应清空
    }
}
