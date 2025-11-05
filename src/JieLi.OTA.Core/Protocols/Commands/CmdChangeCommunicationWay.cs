namespace JieLi.OTA.Core.Protocols.Commands;

/// <summary>切换通信方式命令（对应小程序SDK的 CmdChangeCommunicationWay / CMD_SETTINGS_COMMUNICATION_MTU）</summary>
/// <remarks>
/// SDK原始逻辑：
/// - 在单备份升级模式下，通过此命令告知设备切换通信方式和是否支持新的重启方式
/// - OpCode: 0xD1 (CMD_SETTINGS_COMMUNICATION_MTU)
/// - Payload: [communicationWay, isSupportNewRebootWay (0/1)]
/// </remarks>
public class CmdChangeCommunicationWay : RcspCommand
{
    /// <summary>通信方式（0=默认, 1=其他方式）</summary>
    public byte CommunicationWay { get; set; }

    /// <summary>是否支持新的重启方式（true=支持新广播重连, false=旧方式）</summary>
    public bool IsSupportNewRebootWay { get; set; }

    public override byte OpCode => OtaOpCode.CMD_CHANGE_COMMUNICATION_WAY;

    protected override byte[] SerializePayload()
    {
        // Payload: 2 字节
        // [0] = communicationWay
        // [1] = isSupportNewRebootWay (0 或 1)
        return
        [
            CommunicationWay,
            (byte)(IsSupportNewRebootWay ? 1 : 0)
        ];
    }
}
