# OTA数据结构设计

## 一、RCSP协议基础类

### 1.1 RcspPacket - RCSP数据包

```csharp
namespace Avalonia.Ble.Protocols.Rcsp;

/// <summary>RCSP数据包</summary>
public class RcspPacket
{
    /// <summary>帧头</summary>
    public static readonly byte[] RCSP_HEAD = [0xAA, 0x55];
    
    /// <summary>帧尾</summary>
    public const byte RCSP_END = 0xAD;
    
    /// <summary>序列号</summary>
    public byte Sn { get; set; }
    
    /// <summary>操作码</summary>
    public byte OpCode { get; set; }
    
    /// <summary>是否为命令包</summary>
    public bool IsCommand { get; set; }
    
    /// <summary>是否需要响应</summary>
    public bool NeedResponse { get; set; }
    
    /// <summary>保留字段</summary>
    public byte Reserve { get; set; }
    
    /// <summary>负载数据</summary>
    public byte[]? Payload { get; set; }
    
    /// <summary>标志字节</summary>
    public byte Flag
    {
        get
        {
            byte flag = 0;
            if (IsCommand) flag |= 0x80;
            if (NeedResponse) flag |= 0x40;
            flag |= (byte)(Reserve & 0x3F);
            return flag;
        }
        set
        {
            IsCommand = (value & 0x80) != 0;
            NeedResponse = (value & 0x40) != 0;
            Reserve = (byte)(value & 0x3F);
        }
    }
    
    /// <summary>序列化为字节数组</summary>
    public byte[] ToBytes()
    {
        int length = 6 + (Payload?.Length ?? 0); // HEAD(2) + FLAG(1) + SN(1) + OpCode(1) + Payload + END(1)
        byte[] buffer = new byte[length];
        int offset = 0;
        
        // HEAD
        Buffer.BlockCopy(RCSP_HEAD, 0, buffer, offset, 2);
        offset += 2;
        
        // FLAG
        buffer[offset++] = Flag;
        
        // SN
        buffer[offset++] = Sn;
        
        // OpCode
        buffer[offset++] = OpCode;
        
        // Payload
        if (Payload != null && Payload.Length > 0)
        {
            Buffer.BlockCopy(Payload, 0, buffer, offset, Payload.Length);
            offset += Payload.Length;
        }
        
        // END
        buffer[offset] = RCSP_END;
        
        return buffer;
    }
    
    /// <summary>从字节数组解析</summary>
    public static RcspPacket? Parse(byte[] data)
    {
        if (data == null || data.Length < 6)
            return null;
            
        // 验证帧头
        if (data[0] != RCSP_HEAD[0] || data[1] != RCSP_HEAD[1])
            return null;
            
        // 验证帧尾
        if (data[^1] != RCSP_END)
            return null;
            
        var packet = new RcspPacket
        {
            Flag = data[2],
            Sn = data[3],
            OpCode = data[4]
        };
        
        // 提取Payload
        int payloadLength = data.Length - 6;
        if (payloadLength > 0)
        {
            packet.Payload = new byte[payloadLength];
            Buffer.BlockCopy(data, 5, packet.Payload, 0, payloadLength);
        }
        
        return packet;
    }
}
```

### 1.2 RcspCommand - RCSP命令基类

```csharp
/// <summary>RCSP命令基类</summary>
public abstract class RcspCommand
{
    /// <summary>操作码</summary>
    public abstract byte OpCode { get; }
    
    /// <summary>序列号</summary>
    public byte Sn { get; set; }
    
    /// <summary>是否需要响应</summary>
    public virtual bool NeedResponse => true;
    
    /// <summary>序列化为数据包</summary>
    public virtual RcspPacket ToPacket()
    {
        return new RcspPacket
        {
            OpCode = OpCode,
            Sn = Sn,
            IsCommand = true,
            NeedResponse = NeedResponse,
            Payload = SerializePayload()
        };
    }
    
    /// <summary>序列化负载数据</summary>
    protected abstract byte[]? SerializePayload();
}
```

### 1.3 RcspResponse - RCSP响应基类

```csharp
/// <summary>RCSP响应基类</summary>
public abstract class RcspResponse
{
    /// <summary>成功状态</summary>
    public const byte STATUS_SUCCESS = 0x00;
    
    /// <summary>失败状态</summary>
    public const byte STATUS_FAILED = 0x01;
    
    /// <summary>未知命令</summary>
    public const byte STATUS_UNKNOWN_CMD = 0x02;
    
    /// <summary>系统繁忙</summary>
    public const byte STATUS_BUSY = 0x03;
    
    /// <summary>操作码</summary>
    public byte OpCode { get; set; }
    
    /// <summary>序列号</summary>
    public byte Sn { get; set; }
    
    /// <summary>状态</summary>
    public byte Status { get; set; }
    
    /// <summary>负载数据</summary>
    public byte[]? Payload { get; set; }
    
    /// <summary>是否成功</summary>
    public bool IsSuccess => Status == STATUS_SUCCESS;
    
    /// <summary>从数据包解析</summary>
    public virtual void ParseFromPacket(RcspPacket packet)
    {
        OpCode = packet.OpCode;
        Sn = packet.Sn;
        
        if (packet.Payload != null && packet.Payload.Length > 0)
        {
            Status = packet.Payload[0];
            
            if (packet.Payload.Length > 1)
            {
                Payload = new byte[packet.Payload.Length - 1];
                Buffer.BlockCopy(packet.Payload, 1, Payload, 0, Payload.Length);
                ParsePayload(Payload);
            }
        }
    }
    
    /// <summary>解析负载数据</summary>
    protected virtual void ParsePayload(byte[] payload) { }
}
```

---

## 二、OTA命令定义

### 2.1 OpCode定义

```csharp
/// <summary>OTA操作码</summary>
public static class OtaOpCode
{
    /// <summary>获取设备信息</summary>
    public const byte CMD_GET_TARGET_INFO = 0x02;
    
    /// <summary>读取升级文件偏移</summary>
    public const byte CMD_OTA_GET_FILE_OFFSET = 0xE0;
    
    /// <summary>查询设备是否可升级</summary>
    public const byte CMD_OTA_INQUIRE_CAN_UPDATE = 0xE1;
    
    /// <summary>进入升级模式</summary>
    public const byte CMD_OTA_ENTER_UPDATE_MODE = 0xE2;
    
    /// <summary>退出升级模式</summary>
    public const byte CMD_OTA_EXIT_UPDATE_MODE = 0xE3;
    
    /// <summary>发送文件块</summary>
    public const byte CMD_OTA_SEND_FILE_BLOCK = 0xE4;
    
    /// <summary>查询升级结果</summary>
    public const byte CMD_OTA_QUERY_UPDATE_RESULT = 0xE5;
    
    /// <summary>重启设备</summary>
    public const byte CMD_REBOOT_DEVICE = 0xE6;
    
    /// <summary>通知升级文件大小</summary>
    public const byte CMD_OTA_NOTIFY_FILE_SIZE = 0xE7;
}
```

### 2.2 获取设备信息命令

```csharp
/// <summary>获取设备信息命令</summary>
public class CmdGetTargetInfo : RcspCommand
{
    public override byte OpCode => OtaOpCode.CMD_GET_TARGET_INFO;
    
    /// <summary>掩码</summary>
    public ushort Mask { get; set; } = 0xFFFF;
    
    /// <summary>平台</summary>
    public byte Platform { get; set; } = 0x00;
    
    protected override byte[] SerializePayload()
    {
        return [
            (byte)(Mask & 0xFF),
            (byte)(Mask >> 8),
            Platform
        ];
    }
}

/// <summary>设备信息响应</summary>
public class RspDeviceInfo : RcspResponse
{
    /// <summary>固件版本名称</summary>
    public string? VersionName { get; set; }
    
    /// <summary>固件版本号</summary>
    public int VersionCode { get; set; }
    
    /// <summary>协议版本</summary>
    public string? ProtocolVersion { get; set; }
    
    /// <summary>发送MTU</summary>
    public int SendMtu { get; set; }
    
    /// <summary>接收MTU</summary>
    public int ReceiveMtu { get; set; }
    
    /// <summary>BLE地址</summary>
    public string? BleAddress { get; set; }
    
    /// <summary>设备名称</summary>
    public string? DeviceName { get; set; }
    
    /// <summary>电池电量</summary>
    public byte BatteryLevel { get; set; }
    
    /// <summary>是否支持双备份</summary>
    public bool IsSupportDoubleBackup { get; set; }
    
    /// <summary>是否需要BootLoader</summary>
    public bool IsNeedBootLoader { get; set; }
    
    /// <summary>强制升级标志</summary>
    public byte MandatoryUpgradeFlag { get; set; }
    
    protected override void ParsePayload(byte[] payload)
    {
        // 根据小程序代码实现解析逻辑
        // 详细解析见小程序 ResponseTargetInfo 类
    }
}
```

### 2.3 读取文件偏移命令

```csharp
/// <summary>读取文件偏移命令</summary>
public class CmdReadFileOffset : RcspCommand
{
    public override byte OpCode => OtaOpCode.CMD_OTA_GET_FILE_OFFSET;
    
    protected override byte[]? SerializePayload() => null; // 无负载
}

/// <summary>文件偏移响应</summary>
public class RspFileOffset : RcspResponse
{
    /// <summary>文件偏移</summary>
    public int Offset { get; set; }
    
    /// <summary>数据长度</summary>
    public int Length { get; set; }
    
    protected override void ParsePayload(byte[] payload)
    {
        if (payload.Length >= 8)
        {
            Offset = BitConverter.ToInt32(payload, 0);
            Length = BitConverter.ToInt32(payload, 4);
        }
    }
}
```

### 2.4 查询可升级命令

```csharp
/// <summary>查询设备是否可升级命令</summary>
public class CmdInquireCanUpdate : RcspCommand
{
    public override byte OpCode => OtaOpCode.CMD_OTA_INQUIRE_CAN_UPDATE;
    
    /// <summary>固件数据（用于设备校验）</summary>
    public byte[]? FirmwareData { get; set; }
    
    protected override byte[]? SerializePayload() => FirmwareData;
}

/// <summary>可升级查询结果</summary>
public class RspCanUpdate : RcspResponse
{
    /// <summary>可以升级</summary>
    public const byte RESULT_CAN_UPDATE = 0x00;
    
    /// <summary>设备低电量</summary>
    public const byte RESULT_LOW_VOLTAGE = 0x01;
    
    /// <summary>固件信息错误</summary>
    public const byte RESULT_FIRMWARE_ERROR = 0x02;
    
    /// <summary>版本一致</summary>
    public const byte RESULT_VERSION_NO_CHANGE = 0x03;
    
    /// <summary>TWS未连接</summary>
    public const byte RESULT_TWS_NOT_CONNECT = 0x04;
    
    /// <summary>耳机不在充电仓</summary>
    public const byte RESULT_NOT_IN_CHARGING_BIN = 0x05;
    
    /// <summary>结果</summary>
    public byte Result { get; set; }
    
    protected override void ParsePayload(byte[] payload)
    {
        if (payload.Length > 0)
            Result = payload[0];
    }
}
```

### 2.5 进入升级模式命令

```csharp
/// <summary>进入升级模式命令</summary>
public class CmdEnterUpdateMode : RcspCommand
{
    public override byte OpCode => OtaOpCode.CMD_OTA_ENTER_UPDATE_MODE;
    
    protected override byte[]? SerializePayload() => null;
}

/// <summary>进入升级模式响应</summary>
public class RspEnterUpdateMode : RcspResponse
{
    /// <summary>结果</summary>
    public byte Result { get; set; }
    
    protected override void ParsePayload(byte[] payload)
    {
        if (payload.Length > 0)
            Result = payload[0];
    }
}
```

### 2.6 发送文件块命令（设备主动）

```csharp
/// <summary>设备请求文件块</summary>
public class CmdRequestFileBlock : RcspCommand
{
    public override byte OpCode => OtaOpCode.CMD_OTA_SEND_FILE_BLOCK;
    
    /// <summary>文件偏移</summary>
    public int Offset { get; set; }
    
    /// <summary>数据长度</summary>
    public int Length { get; set; }
    
    protected override byte[] SerializePayload()
    {
        byte[] buffer = new byte[8];
        BitConverter.GetBytes(Offset).CopyTo(buffer, 0);
        BitConverter.GetBytes(Length).CopyTo(buffer, 4);
        return buffer;
    }
}

/// <summary>文件块响应</summary>
public class RspFileBlock : RcspResponse
{
    /// <summary>文件数据块</summary>
    public byte[]? BlockData { get; set; }
    
    protected override void ParsePayload(byte[] payload)
    {
        BlockData = payload;
    }
    
    /// <summary>创建响应</summary>
    public static RcspPacket CreateResponse(byte sn, byte[] blockData)
    {
        byte[] payload = new byte[1 + blockData.Length];
        payload[0] = STATUS_SUCCESS;
        Buffer.BlockCopy(blockData, 0, payload, 1, blockData.Length);
        
        return new RcspPacket
        {
            OpCode = OtaOpCode.CMD_OTA_SEND_FILE_BLOCK,
            Sn = sn,
            IsCommand = false,
            NeedResponse = false,
            Payload = payload
        };
    }
}
```

### 2.7 查询升级结果命令

```csharp
/// <summary>查询升级结果命令</summary>
public class CmdQueryUpdateResult : RcspCommand
{
    public override byte OpCode => OtaOpCode.CMD_OTA_QUERY_UPDATE_RESULT;
    
    protected override byte[]? SerializePayload() => null;
}

/// <summary>升级结果</summary>
public class RspUpdateResult : RcspResponse
{
    /// <summary>升级完成</summary>
    public const byte RESULT_COMPLETE = 0x00;
    
    /// <summary>数据校验错误</summary>
    public const byte RESULT_DATA_CHECK_ERROR = 0x01;
    
    /// <summary>升级失败</summary>
    public const byte RESULT_FAIL = 0x02;
    
    /// <summary>加密密钥不匹配</summary>
    public const byte RESULT_KEY_NOT_MATCH = 0x03;
    
    /// <summary>升级文件错误</summary>
    public const byte RESULT_FILE_ERROR = 0x04;
    
    /// <summary>结果</summary>
    public byte Result { get; set; }
    
    protected override void ParsePayload(byte[] payload)
    {
        if (payload.Length > 0)
            Result = payload[0];
    }
}
```

### 2.8 重启设备命令

```csharp
/// <summary>重启设备命令</summary>
public class CmdRebootDevice : RcspCommand
{
    public override byte OpCode => OtaOpCode.CMD_REBOOT_DEVICE;
    
    /// <summary>重启操作</summary>
    public const byte OP_REBOOT = 0x00;
    
    /// <summary>关闭操作</summary>
    public const byte OP_CLOSE = 0x01;
    
    /// <summary>操作类型</summary>
    public byte Operation { get; set; } = OP_REBOOT;
    
    protected override byte[] SerializePayload() => [Operation];
}
```

### 2.9 通知文件大小命令

```csharp
/// <summary>通知升级文件大小命令</summary>
public class CmdNotifyFileSize : RcspCommand
{
    public override byte OpCode => OtaOpCode.CMD_OTA_NOTIFY_FILE_SIZE;
    
    /// <summary>总大小</summary>
    public int TotalSize { get; set; }
    
    /// <summary>当前大小</summary>
    public int CurrentSize { get; set; }
    
    protected override byte[] SerializePayload()
    {
        byte[] buffer = new byte[8];
        BitConverter.GetBytes(TotalSize).CopyTo(buffer, 0);
        BitConverter.GetBytes(CurrentSize).CopyTo(buffer, 4);
        return buffer;
    }
}
```

---

## 三、OTA业务类

### 3.1 OTA状态枚举

```csharp
/// <summary>OTA升级状态</summary>
public enum OtaState
{
    /// <summary>空闲</summary>
    Idle,
    
    /// <summary>文件校验中</summary>
    FileChecking,
    
    /// <summary>设备连接中</summary>
    DeviceConnecting,
    
    /// <summary>设备认证中</summary>
    DeviceAuthenticating,
    
    /// <summary>RCSP初始化中</summary>
    RcspInitializing,
    
    /// <summary>读取文件偏移</summary>
    ReadingFileOffset,
    
    /// <summary>查询可升级</summary>
    InquiringCanUpdate,
    
    /// <summary>进入升级模式</summary>
    EnteringUpdateMode,
    
    /// <summary>传输中</summary>
    Transferring,
    
    /// <summary>等待回连（单备份）</summary>
    WaitingReconnect,
    
    /// <summary>查询结果</summary>
    QueryingResult,
    
    /// <summary>重启设备</summary>
    Rebooting,
    
    /// <summary>升级成功</summary>
    Success,
    
    /// <summary>升级失败</summary>
    Failed,
    
    /// <summary>升级取消</summary>
    Cancelled
}
```

### 3.2 OTA配置

```csharp
/// <summary>OTA升级配置</summary>
public class OtaConfig
{
    /// <summary>BLE通讯方式</summary>
    public const int COMMUNICATION_WAY_BLE = 0;
    
    /// <summary>SPP通讯方式</summary>
    public const int COMMUNICATION_WAY_SPP = 1;
    
    /// <summary>USB通讯方式</summary>
    public const int COMMUNICATION_WAY_USB = 2;
    
    /// <summary>通讯方式</summary>
    public int CommunicationWay { get; set; } = COMMUNICATION_WAY_BLE;
    
    /// <summary>是否支持新的回连方式</summary>
    public bool IsSupportNewRebootWay { get; set; } = true;
    
    /// <summary>升级文件数据</summary>
    public byte[]? UpdateFileData { get; set; }
    
    /// <summary>升级文件路径</summary>
    public string? UpdateFilePath { get; set; }
}
```

### 3.3 OTA进度信息

```csharp
/// <summary>OTA升级类型</summary>
public enum UpgradeType
{
    /// <summary>未知</summary>
    Unknown = -1,
    
    /// <summary>检查文件</summary>
    CheckFile = 0,
    
    /// <summary>固件升级</summary>
    Firmware = 1
}

/// <summary>OTA进度信息</summary>
public class OtaProgress
{
    /// <summary>升级类型</summary>
    public UpgradeType Type { get; set; }
    
    /// <summary>当前状态</summary>
    public OtaState State { get; set; }
    
    /// <summary>进度百分比（0-100）</summary>
    public int Percentage { get; set; }
    
    /// <summary>已传输字节数</summary>
    public long TransferredBytes { get; set; }
    
    /// <summary>总字节数</summary>
    public long TotalBytes { get; set; }
    
    /// <summary>传输速度（字节/秒）</summary>
    public long TransferSpeed { get; set; }
    
    /// <summary>剩余时间（秒）</summary>
    public int RemainingSeconds { get; set; }
    
    /// <summary>状态描述</summary>
    public string? Message { get; set; }
}
```

### 3.4 OTA错误码

```csharp
/// <summary>OTA错误码</summary>
public static class OtaErrorCode
{
    public const int ERROR_UNKNOWN = -1;
    public const int ERROR_NONE = 0;
    public const int ERROR_INVALID_PARAM = -2;
    public const int ERROR_DATA_FORMAT = -3;
    public const int ERROR_NOT_FOUND_RESOURCE = -4;
    public const int ERROR_UNKNOWN_DEVICE = -32;
    public const int ERROR_DEVICE_OFFLINE = -33;
    public const int ERROR_IO_EXCEPTION = -35;
    public const int ERROR_REPEAT_STATUS = -36;
    public const int ERROR_RESPONSE_TIMEOUT = -64;
    public const int ERROR_REPLY_BAD_STATUS = -65;
    public const int ERROR_REPLY_BAD_RESULT = -66;
    public const int ERROR_NONE_PARSER = -67;
    public const int ERROR_OTA_LOW_POWER = -97;
    public const int ERROR_OTA_UPDATE_FILE = -98;
    public const int ERROR_OTA_FIRMWARE_VERSION_NO_CHANGE = -99;
    public const int ERROR_OTA_TWS_NOT_CONNECT = -100;
    public const int ERROR_OTA_HEADSET_NOT_IN_CHARGING_BIN = -101;
    public const int ERROR_OTA_DATA_CHECK_ERROR = -102;
    public const int ERROR_OTA_FAIL = -103;
    public const int ERROR_OTA_ENCRYPTED_KEY_NOT_MATCH = -104;
    public const int ERROR_OTA_UPGRADE_FILE_ERROR = -105;
    public const int ERROR_OTA_UPGRADE_TYPE_ERROR = -106;
    public const int ERROR_OTA_LENGTH_OVER = -107;
    public const int ERROR_OTA_FLASH_IO_EXCEPTION = -108;
    public const int ERROR_OTA_CMD_TIMEOUT = -109;
    public const int ERROR_OTA_IN_PROGRESS = -110;
    public const int ERROR_OTA_COMMAND_TIMEOUT = -111;
    public const int ERROR_OTA_RECONNECT_DEVICE_TIMEOUT = -112;
    public const int ERROR_OTA_USE_CANCEL = -113;
    public const int ERROR_OTA_SAME_FILE = -114;
    
    /// <summary>获取错误描述</summary>
    public static string GetErrorDescription(int errorCode)
    {
        return errorCode switch
        {
            ERROR_NONE => "成功",
            ERROR_INVALID_PARAM => "无效参数",
            ERROR_DATA_FORMAT => "数据格式错误",
            ERROR_NOT_FOUND_RESOURCE => "未找到资源",
            ERROR_UNKNOWN_DEVICE => "未知设备",
            ERROR_DEVICE_OFFLINE => "设备离线",
            ERROR_IO_EXCEPTION => "IO异常",
            ERROR_RESPONSE_TIMEOUT => "响应超时",
            ERROR_REPLY_BAD_STATUS => "设备返回错误状态",
            ERROR_REPLY_BAD_RESULT => "设备返回错误结果",
            ERROR_OTA_LOW_POWER => "设备电量过低",
            ERROR_OTA_UPDATE_FILE => "升级文件错误",
            ERROR_OTA_FIRMWARE_VERSION_NO_CHANGE => "固件版本未变化",
            ERROR_OTA_TWS_NOT_CONNECT => "TWS未连接",
            ERROR_OTA_HEADSET_NOT_IN_CHARGING_BIN => "耳机不在充电仓",
            ERROR_OTA_DATA_CHECK_ERROR => "数据校验错误",
            ERROR_OTA_FAIL => "升级失败",
            ERROR_OTA_ENCRYPTED_KEY_NOT_MATCH => "加密密钥不匹配",
            ERROR_OTA_UPGRADE_FILE_ERROR => "升级文件错误",
            ERROR_OTA_COMMAND_TIMEOUT => "命令超时",
            ERROR_OTA_RECONNECT_DEVICE_TIMEOUT => "回连设备超时",
            ERROR_OTA_USE_CANCEL => "用户取消",
            _ => $"未知错误 ({errorCode})"
        };
    }
}
```

### 3.5 回连信息

```csharp
/// <summary>回连信息</summary>
public class ReconnectInfo
{
    /// <summary>是否支持新的回连广播</summary>
    public bool IsSupportNewReconnectAdv { get; set; }
    
    /// <summary>设备BLE MAC地址</summary>
    public string? DeviceBleMac { get; set; }
    
    /// <summary>原设备ID</summary>
    public string? OriginalDeviceId { get; set; }
    
    /// <summary>回连超时时间（毫秒）</summary>
    public int TimeoutMilliseconds { get; set; } = 30000;
}
```

---

## 四、回调接口定义

### 4.1 OTA升级回调

```csharp
/// <summary>OTA升级回调接口</summary>
public interface IOtaCallback
{
    /// <summary>OTA开始</summary>
    void OnStartOta();
    
    /// <summary>需要回连（单备份）</summary>
    /// <param name="reconnectInfo">回连信息</param>
    void OnNeedReconnect(ReconnectInfo reconnectInfo);
    
    /// <summary>进度更新</summary>
    /// <param name="progress">进度信息</param>
    void OnProgress(OtaProgress progress);
    
    /// <summary>OTA结束</summary>
    void OnStopOta();
    
    /// <summary>OTA取消</summary>
    void OnCancelOta();
    
    /// <summary>OTA错误</summary>
    /// <param name="errorCode">错误码</param>
    /// <param name="message">错误信息</param>
    void OnError(int errorCode, string message);
}
```

### 4.2 RCSP数据回调

```csharp
/// <summary>RCSP数据回调接口</summary>
public interface IRcspCallback
{
    /// <summary>RCSP初始化完成</summary>
    /// <param name="deviceInfo">设备信息</param>
    void OnRcspInit(RspDeviceInfo deviceInfo);
    
    /// <summary>接收到命令</summary>
    /// <param name="command">命令</param>
    void OnRcspCommand(RcspCommand command);
    
    /// <summary>接收到响应</summary>
    /// <param name="response">响应</param>
    void OnRcspResponse(RcspResponse response);
    
    /// <summary>RCSP错误</summary>
    /// <param name="errorCode">错误码</param>
    /// <param name="message">错误信息</param>
    void OnRcspError(int errorCode, string message);
}
```

---

## 五、服务接口定义

### 5.1 OTA管理器接口

```csharp
/// <summary>OTA管理器接口</summary>
public interface IOtaManager
{
    /// <summary>当前状态</summary>
    OtaState CurrentState { get; }
    
    /// <summary>是否正在升级</summary>
    bool IsUpgrading { get; }
    
    /// <summary>开始OTA升级</summary>
    /// <param name="device">目标设备</param>
    /// <param name="config">升级配置</param>
    /// <param name="callback">升级回调</param>
    Task StartOtaAsync(BleDeviceInfo device, OtaConfig config, IOtaCallback callback);
    
    /// <summary>取消OTA升级（仅双备份支持）</summary>
    bool CancelOta();
    
    /// <summary>释放资源</summary>
    void Release();
}
```

### 5.2 RCSP协议接口

```csharp
/// <summary>RCSP协议接口</summary>
public interface IRcspProtocol
{
    /// <summary>设备是否已连接</summary>
    bool IsDeviceConnected { get; }
    
    /// <summary>当前设备信息</summary>
    RspDeviceInfo? CurrentDeviceInfo { get; }
    
    /// <summary>初始化RCSP</summary>
    Task<RspDeviceInfo> InitializeAsync(BleDeviceInfo device);
    
    /// <summary>发送命令</summary>
    Task<TResponse> SendCommandAsync<TResponse>(RcspCommand command, int timeoutMs = 5000)
        where TResponse : RcspResponse, new();
    
    /// <summary>注册回调</summary>
    void RegisterCallback(IRcspCallback callback);
    
    /// <summary>注销回调</summary>
    void UnregisterCallback(IRcspCallback callback);
    
    /// <summary>释放资源</summary>
    void Release();
}
```

---

**文档版本**: v1.0  
**创建日期**: 2025-11-04  
**维护人**: PeiKeSmart Team
