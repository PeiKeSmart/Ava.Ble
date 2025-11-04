using JieLi.OTA.Infrastructure.FileSystem;

namespace JieLi.OTA.Tests.Infrastructure;

public class OtaFileServiceTests
{
    readonly OtaFileService _service = new();

    #region ValidateFile Tests

    [Fact(DisplayName = "验证不存在的文件应返回失败")]
    public void ValidateFile_NonExistentFile_ShouldReturnFalse()
    {
        // Arrange
        var filePath = "non_existent_file.ufw";

        // Act
        var (isValid, message, _) = _service.ValidateFile(filePath);

        // Assert
        Assert.False(isValid);
        Assert.Equal("文件不存在", message);
    }

    [Fact(DisplayName = "验证空文件应返回失败")]
    public void ValidateFile_EmptyFile_ShouldReturnFalse()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            var (isValid, message, _) = _service.ValidateFile(tempFile);

            // Assert
            Assert.False(isValid);
            Assert.Equal("文件为空", message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact(DisplayName = "验证有效文件应返回成功")]
    public void ValidateFile_ValidFile_ShouldReturnTrue()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), "test_firmware.ufw");
        var testData = new byte[] { 0xAA, 0x55, 0x01, 0x02, 0x03, 0x04, 0xAD };
        File.WriteAllBytes(tempFile, testData);

        try
        {
            // Act
            var (isValid, message, fileData) = _service.ValidateFile(tempFile);

            // Assert
            Assert.True(isValid);
            Assert.Equal("验证成功", message);
            Assert.NotNull(fileData);
            Assert.Equal(testData, fileData);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region ReadFileBlock Tests

    [Fact(DisplayName = "读取文件块应返回正确数据")]
    public void ReadFileBlock_ValidRange_ShouldReturnCorrectData()
    {
        // Arrange
        byte[] fileData = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09];
        
        // Act
        var block = _service.ReadFileBlock(fileData, 2, 4);

        // Assert
        Assert.Equal(4, block.Length);
        Assert.Equal(new byte[] { 0x02, 0x03, 0x04, 0x05 }, block);
    }

    [Fact(DisplayName = "读取超出范围的块应自动截断")]
    public void ReadFileBlock_ExceedsLength_ShouldTruncate()
    {
        // Arrange
        byte[] fileData = [0x00, 0x01, 0x02, 0x03, 0x04];

        // Act
        var block = _service.ReadFileBlock(fileData, 3, 10);

        // Assert
        Assert.Equal(2, block.Length);
        Assert.Equal(new byte[] { 0x03, 0x04 }, block);
    }

    [Fact(DisplayName = "读取负偏移量应抛出异常")]
    public void ReadFileBlock_NegativeOffset_ShouldThrowException()
    {
        // Arrange
        byte[] fileData = [0x00, 0x01, 0x02];

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.ReadFileBlock(fileData, -1, 2));
    }

    #endregion

    #region CRC16 Tests

    [Fact(DisplayName = "计算空数组的 CRC16")]
    public void CalculateCrc16_EmptyArray_ShouldReturnInitialValue()
    {
        // Arrange
        byte[] data = [];

        // Act
        var crc = _service.CalculateCrc16(data);

        // Assert
        Assert.Equal((ushort)0xFFFF, crc);
    }

    [Fact(DisplayName = "计算已知数据的 CRC16")]
    public void CalculateCrc16_KnownData_ShouldReturnExpectedValue()
    {
        // Arrange
        byte[] data = [0x01, 0x02, 0x03, 0x04, 0x05];

        // Act
        var crc = _service.CalculateCrc16(data);

        // Assert  
        // CRC-16-MODBUS 实际结果
        Assert.Equal((ushort)47914, crc);
    }

    [Fact(DisplayName = "计算部分数据的 CRC16")]
    public void CalculateCrc16_PartialData_ShouldReturnCorrectValue()
    {
        // Arrange
        byte[] data = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0xFF];

        // Act
        var crc = _service.CalculateCrc16(data, 1, 5);

        // Assert
        Assert.Equal((ushort)47914, crc);
    }

    #endregion

    #region GetFileInfo Tests

    [Fact(DisplayName = "获取文件信息应返回正确属性")]
    public void GetFileInfo_ValidData_ShouldReturnCorrectInfo()
    {
        // Arrange
        byte[] data = [0x01, 0x02, 0x03, 0x04, 0x05];

        // Act
        var fileInfo = _service.GetFileInfo(data);

        // Assert
        Assert.Equal(5, fileInfo.Size);
        Assert.Equal((ushort)47914, fileInfo.Crc16);
        Assert.Equal(data, fileInfo.Header);
    }

    [Fact(DisplayName = "获取大文件信息应只保留前 256 字节头")]
    public void GetFileInfo_LargeFile_ShouldTruncateHeader()
    {
        // Arrange
        var data = new byte[1024];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }

        // Act
        var fileInfo = _service.GetFileInfo(data);

        // Assert
        Assert.Equal(1024, fileInfo.Size);
        Assert.Equal(256, fileInfo.Header.Length);
        Assert.Equal(data.Take(256).ToArray(), fileInfo.Header);
    }

    #endregion
}
