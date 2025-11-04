using NewLife.Log;

namespace JieLi.OTA.Core.Protocols.Responses;

/// <summary>设备信息响应</summary>
public class RspDeviceInfo : RcspResponse
{
    /// <summary>设备名称</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>固件版本名称</summary>
    public string VersionName { get; set; } = string.Empty;

    /// <summary>固件版本号</summary>
    public uint VersionCode { get; set; }

    /// <summary>设备类型</summary>
    public byte DeviceType { get; set; }

    /// <summary>电池电量 (0-100)</summary>
    public byte BatteryLevel { get; set; }

    /// <summary>是否支持双备份</summary>
    public bool IsSupportDoubleBackup { get; set; }

    /// <summary>设备蓝牙 MAC 地址</summary>
    public string BleMac { get; set; } = string.Empty;

    /// <summary>通信方式（0x01=单备份, 0x02=双备份）</summary>
    public byte CommunicationWay { get; set; }

    protected override void ParsePayload(byte[] payload)
    {
        if (payload.Length < 10)
            return;
        
        try
        {
            int offset = 0;
            
            // 设备名称长度
            byte nameLength = payload[offset++];
            if (offset + nameLength <= payload.Length)
            {
                DeviceName = System.Text.Encoding.UTF8.GetString(payload, offset, nameLength);
                offset += nameLength;
            }
            
            // 版本名称长度
            if (offset < payload.Length)
            {
                byte versionLength = payload[offset++];
                if (offset + versionLength <= payload.Length)
                {
                    VersionName = System.Text.Encoding.UTF8.GetString(payload, offset, versionLength);
                    offset += versionLength;
                }
            }
            
            // 版本号 (4字节，小端序)
            if (offset + 4 <= payload.Length)
            {
                VersionCode = BitConverter.ToUInt32(payload, offset);
                offset += 4;
            }
            
            // 设备类型
            if (offset < payload.Length)
            {
                DeviceType = payload[offset++];
            }
            
            // 电池电量
            if (offset < payload.Length)
            {
                BatteryLevel = payload[offset++];
            }
            
            // 双备份标志
            if (offset < payload.Length)
            {
                IsSupportDoubleBackup = payload[offset++] == 0x01;
            }
            
            // MAC 地址 (6字节)
            if (offset + 6 <= payload.Length)
            {
                BleMac = $"{payload[offset]:X2}:{payload[offset + 1]:X2}:{payload[offset + 2]:X2}:" +
                         $"{payload[offset + 3]:X2}:{payload[offset + 4]:X2}:{payload[offset + 5]:X2}";
                offset += 6;
            }
            
            // 通信方式
            if (offset < payload.Length)
            {
                CommunicationWay = payload[offset];
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    public override string ToString()
    {
        return $"Device: {DeviceName}, Version: {VersionName} ({VersionCode}), Battery: {BatteryLevel}%, " +
               $"DoubleBackup: {IsSupportDoubleBackup}, MAC: {BleMac}";
    }
}
