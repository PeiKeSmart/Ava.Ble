namespace JieLi.OTA.Core.Protocols.Commands;

/// <summary>通知文件大小命令</summary>
public class CmdNotifyFileSize : RcspCommand
{
    /// <summary>文件总大小</summary>
    public uint TotalSize { get; set; }

    /// <summary>当前偏移</summary>
    public uint CurrentOffset { get; set; }

    public override byte OpCode => OtaOpCode.CMD_OTA_NOTIFY_FILE_SIZE;

    protected override byte[] SerializePayload()
    {
        var payload = new byte[8];
        
        // TotalSize (4字节，小端序)
        payload[0] = (byte)(TotalSize & 0xFF);
        payload[1] = (byte)((TotalSize >> 8) & 0xFF);
        payload[2] = (byte)((TotalSize >> 16) & 0xFF);
        payload[3] = (byte)((TotalSize >> 24) & 0xFF);
        
        // CurrentOffset (4字节，小端序)
        payload[4] = (byte)(CurrentOffset & 0xFF);
        payload[5] = (byte)((CurrentOffset >> 8) & 0xFF);
        payload[6] = (byte)((CurrentOffset >> 16) & 0xFF);
        payload[7] = (byte)((CurrentOffset >> 24) & 0xFF);
        
        return payload;
    }
}
