namespace JieLi.OTA.Core.Protocols.Commands;

/// <summary>退出升级模式命令(对应SDK: class tt extends x)</summary>
/// <remarks>
/// SDK定义: class tt extends x{constructor(){super(K.CMD_OTA_EXIT_UPDATE_MODE,new D,new m)}}
/// OpCode: 0xE4 (228)
/// 无Payload参数
/// 响应: RspCanUpdate (m类, result字段)
/// 用途: 双备份模式下取消OTA升级时调用,退出升级模式
/// </remarks>
public class CmdExitUpdateMode : RcspCommand
{
    public override byte OpCode => OtaOpCode.CMD_OTA_EXIT_UPDATE_MODE;

    protected override byte[] SerializePayload()
    {
        return []; // 无 Payload (对应SDK的 new D - 空参数)
    }
}
