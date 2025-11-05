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

    /// <summary>是否需要 BootLoader</summary>
    public bool IsNeedBootLoader { get; set; }

    /// <summary>单备份 OTA 方式</summary>
    public byte SingleBackupOtaWay { get; set; }

    /// <summary>强制升级标志（0=非强制, 1=强制）</summary>
    public byte MandatoryUpgradeFlag { get; set; }

    /// <summary>是否强制升级（根据 MandatoryUpgradeFlag 计算）</summary>
    public bool IsMandatoryUpgrade => MandatoryUpgradeFlag == 1;

    /// <summary>请求 OTA 标志</summary>
    public byte RequestOtaFlag { get; set; }

    /// <summary>扩展模式</summary>
    public byte ExpandMode { get; set; }

    /// <summary>设备蓝牙 MAC 地址</summary>
    public string BleMac { get; set; } = string.Empty;

    /// <summary>通信方式（0x01=单备份, 0x02=双备份）</summary>
    public byte CommunicationWay { get; set; }

    protected override void ParsePayload(byte[] payload)
    {
        if (payload.Length < 3)
            return;
        
        try
        {
            int offset = 0;

            // ⚠️ TLV格式解析 (对应小程序SDK的 case 8/case 9 等)
            while (offset + 2 <= payload.Length)
            {
                byte type = payload[offset++];      // TLV Type
                byte length = payload[offset++];    // TLV Length

                if (offset + length > payload.Length)
                    break;

                byte[] value = new byte[length];
                Array.Copy(payload, offset, value, 0, length);
                offset += length;

                // 根据 Type 解析不同的字段
                switch (type)
                {
                    case 1: // 设备名称
                        if (length > 0)
                            DeviceName = System.Text.Encoding.UTF8.GetString(value);
                        break;

                    case 2: // 固件版本字符串
                        if (length > 0)
                            VersionName = System.Text.Encoding.UTF8.GetString(value);
                        break;

                    case 5: // 版本号 (2字节)
                        if (length >= 2)
                        {
                            ushort versionCode = (ushort)((value[0] << 8) | value[1]);
                            VersionCode = versionCode;
                            // ⚠️ 仅在 VersionName 为空时才格式化版本号
                            if (string.IsNullOrEmpty(VersionName))
                            {
                                var major = (versionCode >> 12) & 0xF;
                                var minor = (versionCode >> 8) & 0xF;
                                var patch = (versionCode >> 4) & 0xF;
                                var build = versionCode & 0xF;
                                VersionName = $"V_{major}.{minor}.{patch}.{build}";
                            }
                        }
                        break;

                    case 6: // SDK 类型
                        if (length >= 1)
                            DeviceType = value[0];
                        break;

                    case 8: // 双备份和BootLoader信息 ⚠️ 关键字段
                        if (length >= 1)
                            IsSupportDoubleBackup = (value[0] & 0xFF) == 1;
                        if (length >= 2)
                            IsNeedBootLoader = (value[1] & 0xFF) == 1;
                        if (length >= 3)
                            SingleBackupOtaWay = value[2];
                        break;

                    case 9: // 强制升级标志 ⚠️ 关键字段
                        if (length >= 1)
                            MandatoryUpgradeFlag = value[0];
                        if (length >= 2)
                            RequestOtaFlag = value[1];
                        if (length >= 3)
                            ExpandMode = value[2];
                        break;

                    case 13: // MTU
                        // sendMtu 和 receiveMtu (可选)
                        break;

                    case 21: // 电池电量
                        if (length >= 1)
                            BatteryLevel = value[0];
                        break;

                    case 22: // MAC 地址 (6字节)
                        if (length >= 6)
                        {
                            BleMac = $"{value[0]:X2}:{value[1]:X2}:{value[2]:X2}:" +
                                     $"{value[3]:X2}:{value[4]:X2}:{value[5]:X2}";
                        }
                        break;

                    default:
                        // 忽略未知类型
                        break;
                }
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
               $"DoubleBackup: {IsSupportDoubleBackup}, NeedBootLoader: {IsNeedBootLoader}, " +
               $"Mandatory: {IsMandatoryUpgrade}, MAC: {BleMac}";
    }
}
