using NewLife.Log;

namespace JieLi.OTA.Infrastructure.FileSystem;

/// <summary>OTA 文件服务</summary>
public class OtaFileService
{
    /// <summary>验证固件文件</summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>验证结果</returns>
    public (bool IsValid, string Message, byte[]? FileData) ValidateFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return (false, "文件不存在", null);
            }

            var info = new System.IO.FileInfo(filePath);
            if (info.Length == 0)
            {
                return (false, "文件为空", null);
            }

            if (info.Length > 50 * 1024 * 1024) // 限制 50MB
            {
                return (false, "文件过大(超过 50MB)", null);
            }

            // 读取文件
            byte[] fileData = File.ReadAllBytes(filePath);

            // 检查文件扩展名
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext != ".ufw" && ext != ".bin")
            {
                XTrace.WriteLine($"[OtaFileService] 警告: 文件扩展名不是 .ufw 或 .bin: {ext}");
            }

            XTrace.WriteLine($"[OtaFileService] 文件验证成功: {filePath}, 大小: {fileData.Length} bytes");
            return (true, "验证成功", fileData);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return (false, $"读取文件失败: {ex.Message}", null);
        }
    }

    /// <summary>读取文件块</summary>
    /// <param name="fileData">文件数据</param>
    /// <param name="offset">偏移量</param>
    /// <param name="length">长度</param>
    /// <returns>文件块数据</returns>
    public byte[] ReadFileBlock(byte[] fileData, int offset, int length)
    {
        if (offset < 0 || offset >= fileData.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "偏移量超出范围");
        }

        int actualLength = Math.Min(length, fileData.Length - offset);
        byte[] block = new byte[actualLength];
        Buffer.BlockCopy(fileData, offset, block, 0, actualLength);

        return block;
    }

    /// <summary>计算 CRC16</summary>
    /// <param name="data">数据</param>
    /// <returns>CRC16 值</returns>
    public ushort CalculateCrc16(byte[] data)
    {
        return CalculateCrc16(data, 0, data.Length);
    }

    /// <summary>计算 CRC16</summary>
    /// <param name="data">数据</param>
    /// <param name="offset">起始偏移</param>
    /// <param name="length">长度</param>
    /// <returns>CRC16 值</returns>
    public ushort CalculateCrc16(byte[] data, int offset, int length)
    {
        ushort crc = 0xFFFF;
        const ushort polynomial = 0xA001; // CRC-16-MODBUS 多项式

        for (int i = offset; i < offset + length; i++)
        {
            crc ^= data[i];

            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x0001) != 0)
                {
                    crc >>= 1;
                    crc ^= polynomial;
                }
                else
                {
                    crc >>= 1;
                }
            }
        }

        return crc;
    }

    /// <summary>获取文件信息摘要</summary>
    /// <param name="fileData">文件数据</param>
    /// <returns>文件信息</returns>
    public FileInfo GetFileInfo(byte[] fileData)
    {
        var crc = CalculateCrc16(fileData);

        return new FileInfo
        {
            Size = fileData.Length,
            Crc16 = crc,
            Header = fileData.Length >= 256 ? fileData.Take(256).ToArray() : fileData
        };
    }

    /// <summary>文件信息</summary>
    public class FileInfo
    {
        /// <summary>文件大小</summary>
        public int Size { get; set; }

        /// <summary>CRC16 校验值</summary>
        public ushort Crc16 { get; set; }

        /// <summary>文件头（前 256 字节）</summary>
        public byte[] Header { get; set; } = [];

        public override string ToString()
        {
            return $"Size: {Size} bytes, CRC16: 0x{Crc16:X4}";
        }
    }
}
