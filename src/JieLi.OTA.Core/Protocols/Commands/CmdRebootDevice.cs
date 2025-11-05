namespace JieLi.OTA.Core.Protocols.Commands;

/// <summary>重启设备命令（对应SDK的 CmdRebootDevice）</summary>
public class CmdRebootDevice : RcspCommand
{
    /// <summary>重启操作类型</summary>
    public const byte OP_REBOOT = 0x01;

    private readonly byte _operation;

    /// <summary>初始化重启设备命令</summary>
    /// <param name="operation">操作类型，默认为 OP_REBOOT (0x01)</param>
    public CmdRebootDevice(byte operation = OP_REBOOT)
    {
        _operation = operation;
    }

    public override byte OpCode => OtaOpCode.CMD_OTA_REBOOT_DEVICE;

    protected override byte[] SerializePayload()
    {
        // 对应SDK: new t.ParamRebootDevice(t.ParamRebootDevice.OP_REBOOT)
        // Payload 包含一个字节的操作类型
        return [_operation];
    }
}
