namespace JieLi.OTA.Core.Protocols.Commands;

/// <summary>进入升级模式命令</summary>
public class CmdEnterUpdateMode : RcspCommand
{
    public override byte OpCode => OtaOpCode.CMD_OTA_ENTER_UPDATE_MODE;

    protected override byte[] SerializePayload()
    {
        return []; // 无 Payload
    }
}
