using NewLife.Log;

namespace JieLi.OTA.Core.Protocols.Responses;

/// <summary>设备信息响应</summary>
public class RspDeviceInfo : RcspResponse
{
    /// <summary>设备名称(对应SDK case 16: name)</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>固件版本名称</summary>
    public string VersionName { get; set; } = string.Empty;

    /// <summary>固件版本号</summary>
    public uint VersionCode { get; set; }

    /// <summary>设备类型</summary>
    public byte DeviceType { get; set; }

    /// <summary>电池电量(对应SDK case 1: quantity, 0-100)</summary>
    public byte BatteryLevel { get; set; }

    /// <summary>音量(对应SDK case 1: volume)</summary>
    public byte Volume { get; set; }

    /// <summary>最大音量(对应SDK case 1: maxVol)</summary>
    public byte MaxVolume { get; set; }

    /// <summary>是否支持音量同步(对应SDK case 1: supportVolumeSync)</summary>
    public bool SupportVolumeSync { get; set; }

    /// <summary>EDR蓝牙地址(对应SDK case 2: edrAddr)</summary>
    public string EdrAddress { get; set; } = string.Empty;

    /// <summary>EDR配置文件(对应SDK case 2: edrProfile)</summary>
    public byte EdrProfile { get; set; }

    /// <summary>EDR状态(对应SDK case 2: edrStatus)</summary>
    public byte EdrStatus { get; set; }

    /// <summary>是否支持包CRC16(对应SDK case 21: supportPackageCrc16)</summary>
    public bool SupportPackageCrc16 { get; set; }

    /// <summary>是否支持按文件名从设备获取文件(对应SDK case 21: getFileByNameWithDev)</summary>
    public bool GetFileByNameWithDev { get; set; }

    /// <summary>是否通过小文件传输联系人(对应SDK case 21: contactsTransferBySmallFile)</summary>
    public bool ContactsTransferBySmallFile { get; set; }

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

    /// <summary>通信方式（对应小程序SDK的 case 3 第一个字节）</summary>
    /// <remarks>
    /// 0 = BLE (蓝牙低功耗)
    /// 1 = SPP (串口协议)
    /// 2 = USB
    /// 默认 0 (BLE)
    /// </remarks>
    public byte CommunicationWay { get; set; } = 0;

    /// <summary>是否支持新的重启广播方式（对应小程序SDK的 isSupportNewRebootWay）</summary>
    /// <remarks>
    /// SDK说明：用于判断设备是否支持新的断开重连广播方式。
    /// - true: 设备支持新方式，断开后会发送特定广播
    /// - false: 设备使用旧方式
    /// 默认 false
    /// </remarks>
    public bool IsSupportNewRebootWay { get; set; }

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
                    case 1: // 电量+音量+同步标志 (对应SDK case 1)
                        // SDK: this.quantity=255&s[0],s.length>2&&(this.volume=255&s[1],this.maxVol=255&s[2]),
                        //      s.length>3&&(this.supportVolumeSync=1==(1&s[3]))
                        if (length >= 1)
                            BatteryLevel = value[0];
                        if (length >= 3)
                        {
                            Volume = value[1];
                            MaxVolume = value[2];
                        }
                        if (length >= 4)
                            SupportVolumeSync = (value[3] & 1) == 1;
                        break;

                    case 2: // EDR地址+profile+状态 (对应SDK case 2)
                        // SDK: this.edrAddr=o(t), this.edrProfile=255&s[6], this.edrStatus=255&s[7]
                        if (length >= 6)
                        {
                            EdrAddress = $"{value[0]:X2}:{value[1]:X2}:{value[2]:X2}:" +
                                        $"{value[3]:X2}:{value[4]:X2}:{value[5]:X2}";
                        }
                        if (length >= 8)
                        {
                            EdrProfile = value[6];
                            EdrStatus = value[7];
                        }
                        break;

                    case 3: // Platform和CommunicationWay (对应SDK的 case 3)
                        // SDK: s.length>1&&(this.platform=s[0],this.license=c(s.slice(1)));
                        // ⚠️ 第一个字节是CommunicationWay(0=BLE, 1=SPP, 2=USB)
                        if (length >= 1)
                        {
                            CommunicationWay = value[0];
                            // value[1..]是license字符串(可选,此处暂不解析)
                        }
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

                    case 16: // 设备名称 (对应SDK case 16: name)
                        // SDK: this.name=String.fromCharCode.apply(null,Array.from(s))
                        if (length > 0)
                            DeviceName = System.Text.Encoding.UTF8.GetString(value);
                        break;

                    case 21: // 包CRC16+文件传输功能 (对应SDK case 21)
                        // SDK: s.length>=4&&(this.supportPackageCrc16=1==(1&s[0]),
                        //      this.getFileByNameWithDev=2==(2&s[0]),
                        //      this.contactsTransferBySmallFile=4==(4&s[0]))
                        if (length >= 1)
                        {
                            SupportPackageCrc16 = (value[0] & 1) == 1;
                            GetFileByNameWithDev = (value[0] & 2) == 2;
                            ContactsTransferBySmallFile = (value[0] & 4) == 4;
                        }
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
        return $"Device: {DeviceName}, Version: {VersionName} ({VersionCode}), " +
               $"Battery: {BatteryLevel}%, Volume: {Volume}/{MaxVolume}, " +
               $"DoubleBackup: {IsSupportDoubleBackup}, NeedBootLoader: {IsNeedBootLoader}, " +
               $"Mandatory: {IsMandatoryUpgrade}, MAC: {BleMac}, EDR: {EdrAddress}";
    }
}
