# JieLi OTA API 文档

## 📚 目录

- [Core 层 API](#core-层-api)
  - [协议模型](#协议模型)
  - [命令](#命令)
  - [响应](#响应)
- [Infrastructure 层 API](#infrastructure-层-api)
  - [蓝牙服务](#蓝牙服务)
  - [文件服务](#文件服务)
- [Application 层 API](#application-层-api)
  - [OTA 管理器](#ota-管理器)
  - [RCSP 协议服务](#rcsp-协议服务)
  - [重连服务](#重连服务)

---

## Core 层 API

### 协议模型

#### RcspPacket

RCSP 协议数据包基类。

```csharp
public class RcspPacket
{
    /// <summary>帧头 (固定 0xAA55)</summary>
    public const UInt16 Header = 0xAA55;
    
    /// <summary>帧尾 (固定 0xAD)</summary>
    public const Byte Tail = 0xAD;
    
    /// <summary>标志位</summary>
    public Byte Flag { get; set; }
    
    /// <summary>序列号 (用于匹配请求响应)</summary>
    public Byte Sn { get; set; }
    
    /// <summary>操作码</summary>
    public Byte OpCode { get; set; }
    
    /// <summary>数据负载</summary>
    public Byte[] Payload { get; set; }
    
    /// <summary>将数据包序列化为字节数组</summary>
    public Byte[] ToBytes();
    
    /// <summary>从字节数组解析数据包</summary>
    public static RcspPacket Parse(Byte[] data);
}
```

**标志位 (Flag) 说明**:

- `bit 7`: IsCommand (1=命令, 0=响应)
- `bit 6`: NeedResponse (1=需要响应, 0=不需要响应)
- `bits 0-5`: 保留

**常用 Flag 值**:

- `0xC0` (1100 0000) - 需要响应的命令
- `0x80` (1000 0000) - 不需要响应的命令
- `0x40` (0100 0000) - 响应包

#### RcspParser

RCSP 数据包解析器。

```csharp
public class RcspParser
{
    /// <summary>添加接收到的数据</summary>
    /// <param name="data">接收到的原始数据</param>
    public void AddData(Byte[] data);
    
    /// <summary>尝试解析一个完整的数据包</summary>
    /// <param name="packet">解析出的数据包</param>
    /// <returns>是否成功解析</returns>
    public Boolean TryParsePacket(out RcspPacket? packet);
    
    /// <summary>清空缓冲区</summary>
    public void Clear();
}
```

**使用示例**:

```csharp
var parser = new RcspParser();

// 接收数据
parser.AddData(receivedBytes);

// 解析数据包
while (parser.TryParsePacket(out var packet))
{
    Console.WriteLine($"收到数据包: OpCode=0x{packet.OpCode:X2}");
}
```

---

### 命令

#### CmdGetTargetInfo

获取设备信息命令。

```csharp
public class CmdGetTargetInfo : RcspCommand
{
    /// <summary>操作码 0x00</summary>
    public override Byte OpCode => 0x00;
    
    /// <summary>序列化为数据包</summary>
    public override RcspPacket ToPacket(Byte sn);
}
```

**返回数据 (Payload)**:

```
Byte 0-1: 设备类型 (UInt16, Little Endian)
Byte 2:   电量 (0-100)
Byte 3:   充电状态 (0=未充电, 1=充电中)
Byte 4-7: 固件版本 (UInt32, Little Endian)
Byte 8-13: MAC 地址 (6 字节)
```

#### CmdEnterOta

进入 OTA 升级模式命令。

```csharp
public class CmdEnterOta : RcspCommand
{
    /// <summary>操作码 0x01</summary>
    public override Byte OpCode => 0x01;
    
    /// <summary>文件总大小</summary>
    public Int32 FileSize { get; set; }
    
    /// <summary>序列化为数据包</summary>
    public override RcspPacket ToPacket(Byte sn);
}
```

**Payload 格式**:

```
Byte 0-3: 文件总大小 (Int32, Little Endian)
```

#### CmdExitOta

退出 OTA 升级模式命令。

```csharp
public class CmdExitOta : RcspCommand
{
    /// <summary>操作码 0x02</summary>
    public override Byte OpCode => 0x02;
    
    /// <summary>序列化为数据包</summary>
    public override RcspPacket ToPacket(Byte sn);
}
```

#### CmdQueryOtaStatus

查询 OTA 升级状态命令。

```csharp
public class CmdQueryOtaStatus : RcspCommand
{
    /// <summary>操作码 0x03</summary>
    public override Byte OpCode => 0x03;
    
    /// <summary>序列化为数据包</summary>
    public override RcspPacket ToPacket(Byte sn);
}
```

**返回数据**:

```
Byte 0: 状态码
  - 0x00: 空闲
  - 0x01: 升级中
  - 0x02: 升级成功
  - 0x03: 升级失败
Byte 1-4: 当前偏移 (Int32, Little Endian)
```

#### CmdRebootDevice

重启设备命令。

```csharp
public class CmdRebootDevice : RcspCommand
{
    /// <summary>操作码 0x04</summary>
    public override Byte OpCode => 0x04;
    
    /// <summary>序列化为数据包</summary>
    public override RcspPacket ToPacket(Byte sn);
}
```

---

### 响应

#### ResponseGetTargetInfo

获取设备信息响应。

```csharp
public class ResponseGetTargetInfo : RcspResponse
{
    /// <summary>设备类型</summary>
    public UInt16 DeviceType { get; set; }
    
    /// <summary>电量 (0-100)</summary>
    public Byte Battery { get; set; }
    
    /// <summary>充电状态</summary>
    public Boolean IsCharging { get; set; }
    
    /// <summary>固件版本</summary>
    public UInt32 FirmwareVersion { get; set; }
    
    /// <summary>MAC 地址</summary>
    public Byte[] MacAddress { get; set; }
    
    /// <summary>从数据包解析</summary>
    public static ResponseGetTargetInfo Parse(RcspPacket packet);
}
```

#### ResponseEnterOta

进入 OTA 模式响应。

```csharp
public class ResponseEnterOta : RcspResponse
{
    /// <summary>结果码</summary>
    public Byte ResultCode { get; set; }
    
    /// <summary>错误消息</summary>
    public String? ErrorMessage { get; set; }
    
    /// <summary>是否成功</summary>
    public Boolean IsSuccess => ResultCode == 0;
    
    /// <summary>从数据包解析</summary>
    public static ResponseEnterOta Parse(RcspPacket packet);
}
```

**结果码**:

- `0x00`: 成功
- `0x01`: 电量不足
- `0x02`: 设备忙
- `0x03`: 不支持的固件
- `0xFF`: 未知错误

#### ResponseQueryOtaStatus

查询 OTA 状态响应。

```csharp
public class ResponseQueryOtaStatus : RcspResponse
{
    /// <summary>状态</summary>
    public OtaState State { get; set; }
    
    /// <summary>当前偏移</summary>
    public Int32 CurrentOffset { get; set; }
    
    /// <summary>从数据包解析</summary>
    public static ResponseQueryOtaStatus Parse(RcspPacket packet);
}

public enum OtaState
{
    Idle = 0,          // 空闲
    InProgress = 1,    // 升级中
    Success = 2,       // 成功
    Failed = 3         // 失败
}
```

---

## Infrastructure 层 API

### 蓝牙服务

#### IBluetoothService

蓝牙服务接口。

```csharp
public interface IBluetoothService
{
    /// <summary>设备发现事件</summary>
    event EventHandler<BleDevice>? DeviceDiscovered;
    
    /// <summary>设备更新事件</summary>
    event EventHandler<BleDevice>? DeviceUpdated;
    
    /// <summary>设备连接事件</summary>
    event EventHandler<String>? DeviceConnected;
    
    /// <summary>设备断开事件</summary>
    event EventHandler<String>? DeviceDisconnected;
    
    /// <summary>数据接收事件</summary>
    event EventHandler<Byte[]>? DataReceived;
    
    /// <summary>开始扫描设备</summary>
    void StartScan();
    
    /// <summary>停止扫描</summary>
    void StopScan();
    
    /// <summary>连接设备</summary>
    Task<Boolean> ConnectAsync(String deviceId, CancellationToken cancellationToken = default);
    
    /// <summary>断开连接</summary>
    Task DisconnectAsync();
    
    /// <summary>发送数据</summary>
    Task<Boolean> SendDataAsync(Byte[] data, CancellationToken cancellationToken = default);
    
    /// <summary>订阅通知</summary>
    Task<Boolean> SubscribeNotificationAsync();
}
```

#### WindowsBleService

Windows BLE 服务实现。

```csharp
public class WindowsBleService : IBluetoothService, IDisposable
{
    /// <summary>初始化 BLE 服务</summary>
    public WindowsBleService();
    
    /// <summary>获取已发现的设备列表</summary>
    public IReadOnlyList<BleDevice> DiscoveredDevices { get; }
    
    /// <summary>当前连接的设备</summary>
    public BleDevice? ConnectedDevice { get; }
    
    /// <summary>是否正在扫描</summary>
    public Boolean IsScanning { get; }
    
    /// <summary>是否已连接</summary>
    public Boolean IsConnected { get; }
    
    // ... 实现 IBluetoothService 接口
}
```

**使用示例**:

```csharp
var bleService = new WindowsBleService();

// 订阅事件
bleService.DeviceDiscovered += (s, device) => 
{
    Console.WriteLine($"发现设备: {device.DeviceName}");
};

bleService.DataReceived += (s, data) => 
{
    Console.WriteLine($"收到数据: {BitConverter.ToString(data)}");
};

// 扫描设备
bleService.StartScan();
await Task.Delay(5000);
bleService.StopScan();

// 连接设备
var device = bleService.DiscoveredDevices.First();
await bleService.ConnectAsync(device.DeviceId);

// 订阅通知
await bleService.SubscribeNotificationAsync();

// 发送数据
var packet = new RcspPacket { OpCode = 0x00, /* ... */ };
await bleService.SendDataAsync(packet.ToBytes());
```

#### BleDevice

BLE 设备模型。

```csharp
public class BleDevice
{
    /// <summary>设备 ID</summary>
    public String DeviceId { get; }
    
    /// <summary>设备名称</summary>
    public String DeviceName { get; private set; }
    
    /// <summary>信号强度 (dBm)</summary>
    public Int16 Rssi { get; private set; }
    
    /// <summary>蓝牙地址</summary>
    public UInt64 BluetoothAddress { get; }
    
    /// <summary>最后更新时间</summary>
    public DateTime LastSeen { get; private set; }
    
    /// <summary>更新设备信息</summary>
    internal void UpdateInfo(String name, Int16 rssi);
}
```

---

### 文件服务

#### IOtaFileService

OTA 文件服务接口。

```csharp
public interface IOtaFileService
{
    /// <summary>验证文件</summary>
    Task<Boolean> ValidateFileAsync(String filePath);
    
    /// <summary>获取文件大小</summary>
    Int64 GetFileSize(String filePath);
    
    /// <summary>读取文件块</summary>
    Task<Byte[]> ReadBlockAsync(String filePath, Int64 offset, Int32 length);
    
    /// <summary>计算文件 CRC</summary>
    UInt16 CalculateCrc(String filePath);
}
```

#### OtaFileService

OTA 文件服务实现。

```csharp
public class OtaFileService : IOtaFileService
{
    /// <summary>初始化文件服务</summary>
    public OtaFileService();
    
    /// <summary>验证文件格式和完整性</summary>
    public async Task<Boolean> ValidateFileAsync(String filePath);
    
    /// <summary>获取文件大小</summary>
    public Int64 GetFileSize(String filePath);
    
    /// <summary>读取文件块</summary>
    public async Task<Byte[]> ReadBlockAsync(String filePath, Int64 offset, Int32 length);
    
    /// <summary>计算文件 CRC16</summary>
    public UInt16 CalculateCrc(String filePath);
}
```

**使用示例**:

```csharp
var fileService = new OtaFileService();

// 验证文件
if (!await fileService.ValidateFileAsync("firmware.ufw"))
{
    Console.WriteLine("文件验证失败!");
    return;
}

// 获取文件大小
var fileSize = fileService.GetFileSize("firmware.ufw");
Console.WriteLine($"文件大小: {fileSize} 字节");

// 读取文件块
var block = await fileService.ReadBlockAsync("firmware.ufw", offset: 0, length: 512);
```

---

## Application 层 API

### OTA 管理器

#### IOtaManager

OTA 管理器接口。

```csharp
public interface IOtaManager
{
    /// <summary>状态变化事件</summary>
    event EventHandler<OtaState>? StateChanged;
    
    /// <summary>进度变化事件</summary>
    event EventHandler<OtaProgress>? ProgressChanged;
    
    /// <summary>当前状态</summary>
    OtaState CurrentState { get; }
    
    /// <summary>开始 OTA 升级</summary>
    Task<OtaResult> StartOtaAsync(String deviceId, String firmwareFilePath, CancellationToken cancellationToken = default);
    
    /// <summary>取消 OTA 升级</summary>
    Task CancelOtaAsync();
}
```

#### OtaManager

OTA 管理器实现。

```csharp
public class OtaManager : IOtaManager
{
    /// <summary>初始化 OTA 管理器</summary>
    public OtaManager(
        IBluetoothService bluetoothService,
        IOtaFileService fileService,
        IRcspProtocol rcspProtocol,
        IReconnectService reconnectService);
    
    /// <summary>开始 OTA 升级</summary>
    public async Task<OtaResult> StartOtaAsync(
        String deviceId, 
        String firmwareFilePath, 
        CancellationToken cancellationToken = default);
    
    /// <summary>取消升级</summary>
    public async Task CancelOtaAsync();
}
```

**OtaState 枚举**:

```csharp
public enum OtaState
{
    Idle,                   // 空闲
    Connecting,             // 连接设备中
    GettingDeviceInfo,      // 获取设备信息
    ReadingFileOffset,      // 读取文件偏移
    ValidatingFirmware,     // 验证固件
    EnteringUpdateMode,     // 进入升级模式
    TransferringFile,       // 传输文件
    WaitingReconnect,       // 等待回连
    QueryingResult,         // 查询结果
    Rebooting,              // 重启设备
    Completed,              // 完成
    Failed,                 // 失败
    Cancelled               // 已取消
}
```

**OtaProgress 模型**:

```csharp
public class OtaProgress
{
    /// <summary>当前状态</summary>
    public OtaState State { get; set; }
    
    /// <summary>进度百分比 (0-100)</summary>
    public Double Percentage { get; set; }
    
    /// <summary>已传输字节数</summary>
    public Int64 TransferredBytes { get; set; }
    
    /// <summary>总字节数</summary>
    public Int64 TotalBytes { get; set; }
    
    /// <summary>传输速度 (字节/秒)</summary>
    public Double Speed { get; set; }
    
    /// <summary>状态消息</summary>
    public String? Message { get; set; }
}
```

**OtaResult 模型**:

```csharp
public class OtaResult
{
    /// <summary>是否成功</summary>
    public Boolean Success { get; set; }
    
    /// <summary>最终状态</summary>
    public OtaState FinalState { get; set; }
    
    /// <summary>错误消息</summary>
    public String? ErrorMessage { get; set; }
    
    /// <summary>耗时 (毫秒)</summary>
    public Int64 ElapsedMilliseconds { get; set; }
}
```

**使用示例**:

```csharp
var otaManager = new OtaManager(bleService, fileService, rcspProtocol, reconnectService);

// 订阅事件
otaManager.StateChanged += (s, state) => 
{
    Console.WriteLine($"状态: {state}");
};

otaManager.ProgressChanged += (s, progress) => 
{
    Console.WriteLine($"进度: {progress.Percentage:F1}% ({progress.Speed / 1024:F1} KB/s)");
};

// 开始升级
var result = await otaManager.StartOtaAsync("DeviceID", "firmware.ufw");

if (result.Success)
{
    Console.WriteLine($"升级成功! 耗时: {result.ElapsedMilliseconds / 1000}s");
}
else
{
    Console.WriteLine($"升级失败: {result.ErrorMessage}");
}
```

---

### RCSP 协议服务

#### IRcspProtocol

RCSP 协议服务接口。

```csharp
public interface IRcspProtocol
{
    /// <summary>命令响应事件</summary>
    event EventHandler<RcspPacket>? ResponseReceived;
    
    /// <summary>发送命令并等待响应</summary>
    Task<RcspPacket> SendCommandAsync(RcspCommand command, TimeSpan timeout, CancellationToken cancellationToken = default);
    
    /// <summary>发送命令 (不等待响应)</summary>
    Task SendCommandAsync(RcspCommand command);
}
```

#### RcspProtocol

RCSP 协议服务实现。

```csharp
public class RcspProtocol : IRcspProtocol
{
    /// <summary>初始化 RCSP 协议服务</summary>
    public RcspProtocol(IBluetoothService bluetoothService);
    
    /// <summary>发送命令并等待响应</summary>
    public async Task<RcspPacket> SendCommandAsync(
        RcspCommand command, 
        TimeSpan timeout, 
        CancellationToken cancellationToken = default);
    
    /// <summary>发送命令 (不等待响应)</summary>
    public async Task SendCommandAsync(RcspCommand command);
}
```

**使用示例**:

```csharp
var rcspProtocol = new RcspProtocol(bleService);

// 发送获取设备信息命令
var cmd = new CmdGetTargetInfo();
var response = await rcspProtocol.SendCommandAsync(cmd, TimeSpan.FromSeconds(5));
var info = ResponseGetTargetInfo.Parse(response);

Console.WriteLine($"设备型号: 0x{info.DeviceType:X4}");
Console.WriteLine($"电量: {info.Battery}%");
Console.WriteLine($"MAC: {BitConverter.ToString(info.MacAddress)}");
```

---

### 重连服务

#### IReconnectService

重连服务接口。

```csharp
public interface IReconnectService
{
    /// <summary>等待设备重连</summary>
    Task<Boolean> WaitForReconnectAsync(
        UInt64 bluetoothAddress, 
        TimeSpan timeout, 
        CancellationToken cancellationToken = default);
}
```

#### ReconnectService

重连服务实现。

```csharp
public class ReconnectService : IReconnectService
{
    /// <summary>初始化重连服务</summary>
    public ReconnectService(IBluetoothService bluetoothService);
    
    /// <summary>等待设备重连</summary>
    public async Task<Boolean> WaitForReconnectAsync(
        UInt64 bluetoothAddress, 
        TimeSpan timeout, 
        CancellationToken cancellationToken = default);
}
```

**使用示例**:

```csharp
var reconnectService = new ReconnectService(bleService);

// 单备份升级后等待重连
Console.WriteLine("等待设备重启...");
var reconnected = await reconnectService.WaitForReconnectAsync(
    deviceAddress, 
    TimeSpan.FromSeconds(30));

if (reconnected)
{
    Console.WriteLine("设备已重连!");
}
else
{
    Console.WriteLine("重连超时!");
}
```

---

## 附录

### 错误码

| 错误码 | 说明 |
|--------|------|
| 0x00 | 成功 |
| 0x01 | 电量不足 (< 30%) |
| 0x02 | 设备忙 |
| 0x03 | 不支持的固件 |
| 0x04 | 文件格式错误 |
| 0x05 | CRC 校验失败 |
| 0xFF | 未知错误 |

### 超时配置

| 操作 | 默认超时 | 说明 |
|------|---------|------|
| 连接设备 | 10 秒 | 蓝牙连接建立 |
| 发送命令 | 5 秒 | 单个命令响应 |
| 回连等待 | 30 秒 | 单备份重启回连 |
| 总升级超时 | 10 分钟 | 整个升级流程 |

---

**文档版本**: v1.0  
**最后更新**: 2025-11-04  
**适用版本**: JieLi OTA v1.0.0+
