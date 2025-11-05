namespace JieLi.OTA.Core.Protocols.Commands;

/// <summary>通知文件大小命令</summary>
public class CmdNotifyFileSize : RcspCommand
{
    /// <summary>文件总大小</summary>
    public uint TotalSize { get; set; }

    /// <summary>当前偏移（可选，断点续传时使用）</summary>
    public uint? CurrentOffset { get; set; }

    public override byte OpCode => OtaOpCode.CMD_OTA_NOTIFY_FILE_SIZE;

    protected override byte[] SerializePayload()
    {
        // 根据是否有 CurrentOffset 决定载荷长度
        // 仅 TotalSize: 4 字节
        // TotalSize + CurrentOffset: 8 字节
        var hasOffset = CurrentOffset.HasValue;
        var payload = new byte[hasOffset ? 8 : 4];
        
        // TotalSize (4字节，大端序)
        payload[0] = (byte)((TotalSize >> 24) & 0xFF);
        payload[1] = (byte)((TotalSize >> 16) & 0xFF);
        payload[2] = (byte)((TotalSize >> 8) & 0xFF);
        payload[3] = (byte)(TotalSize & 0xFF);
        
        if (hasOffset)
        {
            // CurrentOffset (4字节，大端序)
            var offset = CurrentOffset!.Value;
            payload[4] = (byte)((offset >> 24) & 0xFF);
            payload[5] = (byte)((offset >> 16) & 0xFF);
            payload[6] = (byte)((offset >> 8) & 0xFF);
            payload[7] = (byte)(offset & 0xFF);
        }
        
        return payload;
    }
}
