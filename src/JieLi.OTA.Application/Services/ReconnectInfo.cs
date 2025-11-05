namespace JieLi.OTA.Application.Services;

/// <summary>重连信息（对应小程序SDK的 ReConnectMsg）</summary>
internal class ReconnectInfo
{
    /// <summary>原始设备 MAC 地址</summary>
    public ulong DeviceAddress { get; set; }

    /// <summary>是否使用新 MAC 方法（单备份 +1，双备份 +2）</summary>
    public bool UseNewMacMethod { get; set; }

    /// <summary>创建重连信息副本</summary>
    public ReconnectInfo Copy() => new()
    {
        DeviceAddress = DeviceAddress,
        UseNewMacMethod = UseNewMacMethod
    };
}
