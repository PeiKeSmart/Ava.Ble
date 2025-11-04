namespace JieLi.OTA.Core.Protocols.Commands;

/// <summary>读取文件偏移命令</summary>
public class CmdReadFileOffset : RcspCommand
{
    public override byte OpCode => OtaOpCode.CMD_OTA_READ_FILE_OFFSET;

    protected override byte[] SerializePayload()
    {
        return []; // 无 Payload
    }
}
