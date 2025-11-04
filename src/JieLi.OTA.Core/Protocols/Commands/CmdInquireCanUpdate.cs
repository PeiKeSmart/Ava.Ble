namespace JieLi.OTA.Core.Protocols.Commands;

/// <summary>查询是否可升级命令</summary>
public class CmdInquireCanUpdate : RcspCommand
{
    /// <summary>固件数据（通常是固件文件的头部 256 字节）</summary>
    public byte[] FirmwareData { get; set; } = [];

    public override byte OpCode => OtaOpCode.CMD_OTA_INQUIRE_CAN_UPDATE;

    protected override byte[] SerializePayload()
    {
        return FirmwareData;
    }
}
