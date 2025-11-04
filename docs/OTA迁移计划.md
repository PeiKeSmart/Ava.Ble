# 杰理OTA功能迁移到Windows桌面计划

## 项目概述

将微信小程序中的杰理OTA（空中升级）功能迁移到基于Avalonia的Windows桌面应用程序中，实现对杰理蓝牙设备的固件升级。

**项目周期**: 4-6周  
**技术栈**: C# / .NET 9.0 / Avalonia / Windows BLE API  
**参考源码**: WeChat-Mini-Program-OTA

---

## 一、架构设计

### 1.1 整体架构

```
┌─────────────────────────────────────────────────────┐
│                  Avalonia UI Layer                   │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────┐ │
│  │OTA升级页面   │  │设备管理页面  │  │日志页面   │ │
│  └──────────────┘  └──────────────┘  └───────────┘ │
└─────────────────────────────────────────────────────┘
                         ▲
                         │ MVVM Binding
                         ▼
┌─────────────────────────────────────────────────────┐
│                  ViewModel Layer                     │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────┐ │
│  │OtaViewModel  │  │DeviceVM      │  │LogVM      │ │
│  └──────────────┘  └──────────────┘  └───────────┘ │
└─────────────────────────────────────────────────────┘
                         ▲
                         │ Service Call
                         ▼
┌─────────────────────────────────────────────────────┐
│                   Service Layer                      │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────┐ │
│  │OtaManager    │  │BleService    │  │FileService│ │
│  │(业务逻辑)    │  │(已有)        │  │(文件处理) │ │
│  └──────────────┘  └──────────────┘  └───────────┘ │
└─────────────────────────────────────────────────────┘
                         ▲
                         │ Protocol Implementation
                         ▼
┌─────────────────────────────────────────────────────┐
│                  Protocol Layer                      │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────┐ │
│  │RcspProtocol  │  │AuthService   │  │DataParser │ │
│  │(RCSP协议)    │  │(设备认证)    │  │(数据解析) │ │
│  └──────────────┘  └──────────────┘  └───────────┘ │
└─────────────────────────────────────────────────────┘
                         ▲
                         │ Windows BLE API
                         ▼
┌─────────────────────────────────────────────────────┐
│            Windows.Devices.Bluetooth API             │
└─────────────────────────────────────────────────────┘
```

### 1.2 关键模块职责

| 模块 | 职责 | 对应小程序文件 |
|------|------|---------------|
| **RcspProtocol** | RCSP协议解析、数据包组装拆解 | jl_rcsp_ota_2.1.1.js |
| **OtaManager** | OTA升级流程控制、状态机管理 | jl_ota_2.1.1.js, otaWrapper.ts |
| **AuthService** | 设备认证、密钥交换 | jl_auth_2.0.0.js |
| **ReconnectService** | 设备回连逻辑（单备份升级） | reconnect.ts |
| **BleService** | 蓝牙通信（已有，需扩展） | bluetooth.ts |
| **FileService** | 升级文件解析、验证 | upgradeFileUtil.ts |

---

## 二、RCSP协议实现

### 2.1 协议概述

RCSP（Remote Control Serial Protocol）是杰理自定义的设备通讯协议，用于命令下发和数据传输。

**协议格式**:
```
┌──────┬──────┬──────┬──────┬─────────┬──────┐
│ HEAD │ FLAG │ SN   │OpCode│ Payload │ END  │
│ 2B   │ 1B   │ 1B   │ 1B   │ N Bytes │ 1B   │
└──────┴──────┴──────┴──────┴─────────┴──────┘

HEAD: 0xAA 0x55 (固定帧头)
FLAG: bit7=isCommand, bit6=needResponse
SN:   序列号（用于匹配请求响应）
OpCode: 操作码（见下表）
END:  0xAD (固定帧尾)
```

### 2.2 OTA相关操作码

| OpCode | 名称 | 方向 | 说明 |
|--------|------|------|------|
| 0xE0 | CMD_OTA_GET_FILE_OFFSET | PC→设备 | 读取设备当前升级文件偏移 |
| 0xE1 | CMD_OTA_INQUIRE_CAN_UPDATE | PC→设备 | 查询设备是否可升级 |
| 0xE2 | CMD_OTA_ENTER_UPDATE_MODE | PC→设备 | 进入升级模式 |
| 0xE3 | CMD_OTA_EXIT_UPDATE_MODE | PC→设备 | 退出升级模式 |
| 0xE4 | CMD_OTA_SEND_FILE_BLOCK | 设备→PC | 设备请求文件数据块 |
| 0xE5 | CMD_OTA_QUERY_UPDATE_RESULT | PC→设备 | 查询升级结果 |
| 0xE6 | CMD_REBOOT_DEVICE | PC→设备 | 重启设备 |
| 0xE7 | CMD_OTA_NOTIFY_FILE_SIZE | PC→设备 | 通知升级文件大小 |

### 2.3 数据包示例

**查询设备信息**:
```
发送: AA 55 C0 01 02 00 01 AD
      └─┬─┘ │  │  │  └─┘ └── END
        │   │  │  └───────── OpCode: 0x02 (GetTargetInfo)
        │   │  └──────────── SN: 0x01
        │   └─────────────── FLAG: 0xC0 (isCommand=1, needResponse=1)
        └─────────────────── HEAD

接收: AA 55 40 01 02 [DeviceInfo] AD
      └─┬─┘ │  │  │  └─────────┬──┘
        │   │  │  │            └── 设备信息Payload
        │   │  │  └───────────── OpCode: 0x02
        │   │  └──────────────── SN: 0x01 (匹配请求)
        │   └─────────────────── FLAG: 0x40 (响应包)
        └─────────────────────── HEAD
```

---

## 三、OTA升级流程

### 3.1 完整流程图

```
┌─────────────┐
│  选择文件   │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│  验证文件   │ ← 检查文件格式、完整性
└──────┬──────┘
       │
       ▼
┌─────────────┐
│  扫描设备   │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│  连接设备   │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ 设备认证    │ ← 如果设备开启认证
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ 初始化RCSP  │ ← 获取设备信息、协商MTU
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ 读取文件偏移│ ← 获取设备当前升级状态
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ 查询可升级  │ ← 验证固件版本、电量、TWS状态
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ 进入升级模式│
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ 传输文件    │ ← 设备请求数据块，PC响应
└──────┬──────┘
       │
       ▼
    ┌──┴──┐
    │双备份│
    └──┬──┘
       │
   ┌───┴───┐
   │       │
   ▼       ▼
 [单备份] [双备份]
   │       │
   │       └──────────────┐
   ▼                      ▼
┌─────────────┐    ┌─────────────┐
│ 设备断开    │    │ 查询升级结果│
└──────┬──────┘    └──────┬──────┘
       │                  │
       ▼                  │
┌─────────────┐           │
│ 回连设备    │ ← 等待设备重启后重新连接
└──────┬──────┘           │
       │                  │
       └──────┬───────────┘
              ▼
       ┌─────────────┐
       │ 重启设备    │
       └──────┬──────┘
              │
              ▼
       ┌─────────────┐
       │ 升级完成    │
       └─────────────┘
```

### 3.2 状态机定义

```csharp
public enum OtaState
{
    Idle,                    // 空闲
    FileChecking,            // 文件校验中
    DeviceConnecting,        // 设备连接中
    DeviceAuthenticating,    // 设备认证中
    RcspInitializing,        // RCSP初始化中
    ReadingFileOffset,       // 读取文件偏移
    InquiringCanUpdate,      // 查询可升级
    EnteringUpdateMode,      // 进入升级模式
    Transferring,            // 传输中
    WaitingReconnect,        // 等待回连（单备份）
    QueryingResult,          // 查询结果
    Rebooting,               // 重启设备
    Success,                 // 升级成功
    Failed,                  // 升级失败
    Cancelled                // 升级取消
}
```

### 3.3 关键流程说明

#### 3.3.1 文件传输流程

```
PC端                                    设备端
 │                                        │
 │  CMD_OTA_NOTIFY_FILE_SIZE (总大小)    │
 │ ──────────────────────────────────>   │
 │                                        │
 │         <──── 响应: OK                │
 │                                        │
 │  CMD_OTA_ENTER_UPDATE_MODE            │
 │ ──────────────────────────────────>   │
 │                                        │
 │         <──── 响应: OK                │
 │                                        │
 │  <──── CMD_OTA_SEND_FILE_BLOCK       │
 │        (offset=0, len=512)            │
 │                                        │
 │  响应: [512字节数据] ────────────>     │
 │                                        │
 │  <──── CMD_OTA_SEND_FILE_BLOCK       │
 │        (offset=512, len=512)          │
 │                                        │
 │  响应: [512字节数据] ────────────>     │
 │                                        │
 │  ... 循环直到传输完成 ...             │
 │                                        │
 │  CMD_OTA_QUERY_UPDATE_RESULT          │
 │ ──────────────────────────────────>   │
 │                                        │
 │         <──── 响应: SUCCESS           │
```

#### 3.3.2 单备份回连流程

单备份方案中，设备在升级完成后会重启，PC需要重新扫描并连接设备。

**新回连方式（通过广播包MAC地址匹配）**:
```
1. 升级前，通过RCSP协议获取设备BLE MAC地址
2. 设备重启后，扫描广播包
3. 解析广播包中的特定字段（Type=0xFF，Company ID=0x05D6）
4. 从广播包偏移位置提取MAC地址
5. 匹配MAC地址，确认为目标设备
6. 重新连接
```

**旧回连方式（通过Device ID匹配）**:
```
1. 记录原设备的Device ID
2. 设备重启后，扫描设备
3. 匹配Device ID（部分匹配，取前10位）
4. 重新连接
```

---

## 四、数据结构设计

### 4.1 核心类定义

详见 `docs\OTA数据结构设计.md`

---

## 五、开发计划

### 阶段1: 协议层实现（2-3周）

**目标**: 实现RCSP协议的解析和OTA命令

#### 1.1 创建基础协议类（3天）
- [ ] `RcspPacket.cs` - RCSP数据包基类
- [ ] `RcspCommand.cs` - RCSP命令基类
- [ ] `RcspResponse.cs` - RCSP响应基类
- [ ] `RcspParser.cs` - 数据包解析器

#### 1.2 实现OTA命令（5天）
- [ ] `CmdGetTargetInfo.cs` - 获取设备信息
- [ ] `CmdReadFileOffset.cs` - 读取文件偏移
- [ ] `CmdInquireCanUpdate.cs` - 查询可升级
- [ ] `CmdEnterUpdateMode.cs` - 进入升级模式
- [ ] `CmdSendFileBlock.cs` - 发送文件块
- [ ] `CmdQueryUpdateResult.cs` - 查询升级结果
- [ ] `CmdRebootDevice.cs` - 重启设备

#### 1.3 实现认证服务（3天）
- [ ] `AuthService.cs` - 设备认证逻辑
- [ ] `AuthListener.cs` - 认证事件回调

#### 1.4 单元测试（3天）
- [ ] 数据包解析测试
- [ ] 命令序列化/反序列化测试
- [ ] 认证流程测试

### 阶段2: BLE通信集成（1-2周）

**目标**: 扩展BleService，实现特征值读写和通知订阅

#### 2.1 扩展BleService（4天）
- [ ] 添加特征值写入方法 `WriteCharacteristicAsync`
- [ ] 添加特征值读取方法 `ReadCharacteristicAsync`
- [ ] 添加通知订阅方法 `SubscribeNotificationAsync`
- [ ] 实现数据接收事件 `DataReceived`

#### 2.2 实现数据处理（3天）
- [ ] `RcspDataHandler.cs` - RCSP数据处理器
- [ ] 发送队列管理
- [ ] 接收数据缓冲与组包
- [ ] 超时重传机制

#### 2.3 MTU协商（2天）
- [ ] 实现MTU协商逻辑
- [ ] 根据MTU分包发送

### 阶段3: OTA业务逻辑（1-2周）

**目标**: 实现OTA升级流程控制

#### 3.1 创建OtaManager（5天）
- [ ] `OtaManager.cs` - OTA管理器
- [ ] 状态机实现
- [ ] 升级流程控制
- [ ] 进度计算与回调

#### 3.2 实现回连服务（3天）
- [ ] `ReconnectService.cs` - 回连服务
- [ ] 新旧回连方式支持
- [ ] MAC地址解析
- [ ] 回连超时处理

#### 3.3 实现文件服务（2天）
- [ ] `OtaFileService.cs` - 升级文件处理
- [ ] 文件格式验证
- [ ] 文件块读取
- [ ] CRC16校验

### 阶段4: UI与ViewModel（1-2周）

**目标**: 实现用户界面和交互逻辑

#### 4.1 创建ViewModel（4天）
- [ ] `OtaViewModel.cs` - OTA升级VM
- [ ] `OtaDeviceViewModel.cs` - 设备选择VM
- [ ] 属性绑定与命令定义
- [ ] 进度通知

#### 4.2 创建UI页面（4天）
- [ ] `OtaUpgradePage.axaml` - OTA升级页面
- [ ] `OtaDeviceSelectPage.axaml` - 设备选择页面
- [ ] 进度条、日志显示
- [ ] 交互动画

#### 4.3 集成测试（2天）
- [ ] 完整升级流程测试
- [ ] 异常场景测试
- [ ] UI响应测试

### 阶段5: 测试与优化（1周）

#### 5.1 功能测试（3天）
- [ ] 单备份升级测试
- [ ] 双备份升级测试
- [ ] 回连功能测试
- [ ] 错误处理测试

#### 5.2 性能优化（2天）
- [ ] 传输速度优化
- [ ] 内存占用优化
- [ ] UI响应优化

#### 5.3 文档完善（2天）
- [ ] 用户使用手册
- [ ] API文档
- [ ] 故障排查指南

---

## 六、技术难点与解决方案

### 6.1 MTU协商

**问题**: Windows BLE的MTU协商与小程序不同

**解决方案**:
```csharp
// 使用 GattSession 请求最大MTU
var session = await GattSession.FromDeviceIdAsync(deviceId);
session.MaintainConnection = true;
session.MaxPduSizeChanged += OnMtuChanged;

// 协商MTU（最大512字节）
await session.RequestMaxPduSizeAsync(512);
```

### 6.2 数据分包

**问题**: BLE特征值写入有长度限制（通常20字节）

**解决方案**:
```csharp
// 根据协商的MTU分包发送
private async Task SendLargeDataAsync(byte[] data)
{
    int mtu = _currentMtu - 3; // 减去ATT头部
    for (int i = 0; i < data.Length; i += mtu)
    {
        int length = Math.Min(mtu, data.Length - i);
        byte[] chunk = new byte[length];
        Buffer.BlockCopy(data, i, chunk, 0, length);
        
        await WriteCharacteristicAsync(chunk);
        await Task.Delay(10); // 避免发送过快
    }
}
```

### 6.3 回连超时

**问题**: 单备份升级时，设备重启时间不确定

**解决方案**:
```csharp
// 设置回连超时（默认30秒）
private const int RECONNECT_TIMEOUT = 30000;

// 异步等待回连
private async Task<bool> WaitReconnectAsync(CancellationToken ct)
{
    var startTime = DateTime.Now;
    
    while ((DateTime.Now - startTime).TotalMilliseconds < RECONNECT_TIMEOUT)
    {
        if (ct.IsCancellationRequested)
            return false;
            
        var devices = await ScanDevicesAsync();
        var targetDevice = FindReconnectDevice(devices);
        
        if (targetDevice != null)
        {
            return await ConnectAsync(targetDevice);
        }
        
        await Task.Delay(1000, ct);
    }
    
    return false;
}
```

### 6.4 线程安全

**问题**: 桌面应用是多线程环境，需要保证线程安全

**解决方案**:
```csharp
// 使用lock保护共享资源
private readonly object _lockObj = new object();
private readonly Queue<RcspCommand> _sendQueue = new Queue<RcspCommand>();

public void EnqueueCommand(RcspCommand cmd)
{
    lock (_lockObj)
    {
        _sendQueue.Enqueue(cmd);
    }
}

// UI更新使用Dispatcher
await Dispatcher.UIThread.InvokeAsync(() => {
    Progress = newValue;
});
```

---

## 七、测试计划

### 7.1 单元测试

| 测试模块 | 测试用例 |
|---------|---------|
| RcspPacket | 数据包序列化/反序列化 |
| RcspParser | 各种数据包解析 |
| AuthService | 认证流程模拟 |
| OtaFileService | 文件读取、校验 |

### 7.2 集成测试

| 测试场景 | 预期结果 |
|---------|---------|
| 单备份升级 | 成功升级并回连 |
| 双备份升级 | 成功升级无需回连 |
| 取消升级 | 正确停止并恢复 |
| 断开重连 | 继续升级（双备份） |
| 低电量 | 提示并拒绝升级 |
| 文件错误 | 检测并提示 |

### 7.3 性能测试

| 测试项 | 目标 |
|-------|------|
| 升级速度 | ≥ 10KB/s |
| 内存占用 | ≤ 100MB |
| CPU占用 | ≤ 5% |

---

## 八、风险评估

| 风险 | 影响 | 概率 | 应对措施 |
|------|------|------|---------|
| RCSP协议理解偏差 | 高 | 中 | 详细研究小程序代码，必要时联系厂商 |
| BLE稳定性问题 | 中 | 中 | 增加重试机制，详细日志 |
| 设备兼容性 | 中 | 低 | 多设备测试 |
| 回连失败 | 中 | 中 | 增加超时提示，手动重连 |

---

## 九、参考资源

- **小程序源码**: `WeChat-Mini-Program-OTA/`
- **杰理文档**: [杰理OTA文档系统](https://doc.zh-jieli.com/vue/#/docs/ota)
- **Windows BLE API**: [Microsoft Docs - Bluetooth LE](https://docs.microsoft.com/en-us/windows/uwp/devices-sensors/bluetooth-low-energy-overview)
- **Avalonia文档**: [Avalonia UI Documentation](https://docs.avaloniaui.net/)

---

## 十、后续优化方向

1. **批量升级**: 支持同时升级多个设备
2. **升级历史**: 记录升级记录和统计
3. **固件管理**: 本地固件库管理
4. **自动检测**: 自动检测新固件
5. **远程升级**: 支持远程下载固件

---

**文档版本**: v1.0  
**创建日期**: 2025-11-04  
**维护人**: PeiKeSmart Team
