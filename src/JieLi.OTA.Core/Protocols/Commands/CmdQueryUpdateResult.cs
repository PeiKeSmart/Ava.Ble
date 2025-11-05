namespace JieLi.OTA.Core.Protocols.Commands;

/// <summary>查询升级结果命令 (0xE6)</summary>
public class CmdQueryUpdateResult : RcspCommand
{
    public override byte OpCode => OtaOpCode.CMD_OTA_QUERY_UPDATE_RESULT;

    protected override byte[] SerializePayload()
    {
        // 无业务负载
        return [];
    }
}
