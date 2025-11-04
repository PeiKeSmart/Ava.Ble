using NewLife.Log;

namespace JieLi.OTA.Core.Protocols;

/// <summary>RCSP 数据包解析器</summary>
/// <remarks>
/// 负责从连续的字节流中解析出完整的 RCSP 数据包。
/// 支持数据包分片接收和缓冲区管理。
/// </remarks>
public class RcspParser
{
    private readonly List<byte> _buffer = [];
    private const int MAX_BUFFER_SIZE = 4096;

    /// <summary>添加接收到的数据</summary>
    /// <param name="data">数据字节数组</param>
    public void AddData(byte[] data)
    {
        _buffer.AddRange(data);
        
        // 防止缓冲区无限增长
        if (_buffer.Count > MAX_BUFFER_SIZE)
        {
            XTrace.WriteLine($"[RcspParser] 缓冲区溢出，清空: {_buffer.Count} bytes");
            _buffer.Clear();
        }
    }

    /// <summary>添加接收到的数据</summary>
    /// <param name="data">数据 ReadOnlySpan</param>
    public void AddData(ReadOnlySpan<byte> data)
    {
        _buffer.AddRange(data.ToArray());
        
        if (_buffer.Count > MAX_BUFFER_SIZE)
        {
            XTrace.WriteLine($"[RcspParser] 缓冲区溢出，清空: {_buffer.Count} bytes");
            _buffer.Clear();
        }
    }

    /// <summary>尝试解析一个完整的数据包</summary>
    /// <returns>解析成功返回数据包，否则返回 null</returns>
    public RcspPacket? TryParse()
    {
        // 至少需要最小包长度
        if (_buffer.Count < RcspPacket.MIN_PACKET_LENGTH)
            return null;
        
        // 查找帧头
        int headIndex = FindHead();
        if (headIndex == -1)
        {
            // 未找到帧头，清空无效数据
            _buffer.Clear();
            return null;
        }
        
        // 丢弃帧头之前的数据
        if (headIndex > 0)
        {
            XTrace.WriteLine($"[RcspParser] 丢弃无效数据: {headIndex} bytes");
            _buffer.RemoveRange(0, headIndex);
        }
        
        // 重新检查长度
        if (_buffer.Count < RcspPacket.MIN_PACKET_LENGTH)
            return null;
        
        // 查找帧尾
        int endIndex = _buffer.IndexOf(RcspPacket.RCSP_END, 5); // 从第5个字节开始查找
        if (endIndex == -1)
        {
            // 未找到帧尾，等待更多数据
            return null;
        }
        
        // 提取数据包
        int packetLength = endIndex + 1;
        byte[] packetData = _buffer.Take(packetLength).ToArray();
        
        // 从缓冲区移除已处理数据
        _buffer.RemoveRange(0, packetLength);
        
        // 解析数据包
        var packet = RcspPacket.Parse(packetData);
        if (packet == null)
        {
            XTrace.WriteLine($"[RcspParser] 数据包解析失败: {BitConverter.ToString(packetData)}");
        }
        
        return packet;
    }

    /// <summary>清空缓冲区</summary>
    public void Clear()
    {
        _buffer.Clear();
    }

    /// <summary>获取当前缓冲区大小</summary>
    public int BufferSize => _buffer.Count;

    private int FindHead()
    {
        for (int i = 0; i < _buffer.Count - 1; i++)
        {
            if (_buffer[i] == RcspPacket.RCSP_HEAD_1 && _buffer[i + 1] == RcspPacket.RCSP_HEAD_2)
            {
                return i;
            }
        }
        return -1;
    }
}
