# 快速开始指南

本指南帮助开发者快速上手 JieLi.OTA 项目开发。

## 🏃 5 分钟快速体验

### 1. 克隆项目

```bash
cd h:\Project\Ava.Ble
```

### 2. 编译项目

```bash
dotnet build JieLi.OTA.sln
```

### 3. 运行测试

```bash
dotnet test tests\JieLi.OTA.Tests\JieLi.OTA.Tests.csproj
```

预期输出：
```
测试总数: 17
     通过: 17 ✅
总时间: < 1 秒
```

### 4. 启动应用

```bash
dotnet run --project src\JieLi.OTA.Desktop\JieLi.OTA.Desktop.csproj
```

应用将显示一个欢迎窗口。

---

## 📁 项目结构导览

### Core 层（核心领域）

**位置**: `src/JieLi.OTA.Core/`

这是整个系统的核心，包含：

1. **协议定义** (`Protocols/`)
   - `RcspPacket.cs` - 数据包格式（AA 55 ... AD）
   - `RcspParser.cs` - 解析器（处理分片数据）
   - `Commands/` - 所有 OTA 命令类
   - `Responses/` - 所有响应类

2. **领域模型** (`Models/`)
   - `OtaState.cs` - 升级状态枚举
   - `OtaProgress.cs` - 进度信息
   - `OtaConfig.cs` - 配置项

3. **接口** (`Interfaces/`)
   - `IOtaManager` - OTA 管理器契约
   - `IRcspProtocol` - RCSP 协议契约
   - `IBluetoothDevice` - 蓝牙设备契约

**特点**:
- ✅ 无依赖外部框架（除 NewLife.Core）
- ✅ 纯逻辑，可独立测试
- ✅ 100% 单元测试覆盖

### Infrastructure 层（基础设施）

**位置**: `src/JieLi.OTA.Infrastructure/`

**当前状态**: 🚧 待实现

**计划内容**:
- `Bluetooth/WindowsBleService.cs` - Windows BLE API 封装
- `FileSystem/OtaFileService.cs` - 文件处理服务
- `Logging/XTraceLogger.cs` - 日志适配器

**开发要点**:
- 依赖 Windows.Devices.Bluetooth API
- 实现 Core 层定义的接口
- 处理平台特定逻辑

### Application 层（应用逻辑）

**位置**: `src/JieLi.OTA.Application/`

**当前状态**: 🚧 待实现

**计划内容**:
- `Services/OtaManager.cs` - OTA 升级主流程
- `Services/RcspProtocol.cs` - RCSP 协议服务
- `Services/ReconnectService.cs` - 设备回连逻辑

**开发要点**:
- 协调 Core 和 Infrastructure
- 实现完整业务流程
- 抛出友好的业务异常

### Desktop 层（用户界面）

**位置**: `src/JieLi.OTA.Desktop/`

**当前状态**: ✅ 骨架完成

**已有内容**:
- `Program.cs` - 应用入口
- `App.axaml` - Avalonia 应用定义
- `Views/MainWindow.axaml` - 主窗口

**待实现**:
- `ViewModels/` - 视图模型（MVVM）
- `Views/DeviceScanView.axaml` - 设备扫描页
- `Views/OtaUpgradeView.axaml` - OTA 升级页

---

## 🔨 开发工作流

### 典型开发循环

1. **阅读文档** - 查看 `docs/` 目录相关文档
2. **定义接口** - 在 Core 层创建接口
3. **编写测试** - 在 Tests 项目添加测试用例
4. **实现功能** - 在对应层实现接口
5. **运行测试** - `dotnet test` 验证
6. **提交代码** - 使用规范的 commit 消息

### 示例：添加新命令

假设要添加 `CmdSendFileBlock` 命令：

#### Step 1: 在 Core 层定义命令

**文件**: `src/JieLi.OTA.Core/Protocols/Commands/CmdSendFileBlock.cs`

```csharp
namespace JieLi.OTA.Core.Protocols.Commands;

/// <summary>发送文件块命令</summary>
public class CmdSendFileBlock : RcspCommand
{
    /// <summary>文件偏移</summary>
    public uint Offset { get; set; }

    /// <summary>文件数据</summary>
    public byte[] Data { get; set; } = [];

    public override byte OpCode => OtaOpCode.CMD_OTA_FILE_BLOCK;

    protected override byte[] SerializePayload()
    {
        var payload = new byte[4 + Data.Length];
        
        // Offset (4字节，小端序)
        BitConverter.TryWriteBytes(payload.AsSpan(0, 4), Offset);
        
        // Data
        Buffer.BlockCopy(Data, 0, payload, 4, Data.Length);
        
        return payload;
    }
}
```

#### Step 2: 编写单元测试

**文件**: `tests/JieLi.OTA.Tests/Protocols/Commands/CmdSendFileBlockTests.cs`

```csharp
namespace JieLi.OTA.Tests.Protocols.Commands;

public class CmdSendFileBlockTests
{
    [Fact(DisplayName = "SerializePayload 应正确序列化偏移和数据")]
    public void SerializePayload_ShouldSerializeCorrectly()
    {
        // Arrange
        var cmd = new CmdSendFileBlock
        {
            Offset = 1024,
            Data = [0x01, 0x02, 0x03, 0x04]
        };
        
        // Act
        var packet = cmd.ToPacket(1);
        var payload = packet.Payload;
        
        // Assert
        Assert.Equal(8, payload.Length); // 4 + 4
        Assert.Equal(1024u, BitConverter.ToUInt32(payload, 0));
        Assert.Equal([0x01, 0x02, 0x03, 0x04], payload.Skip(4).ToArray());
    }
}
```

#### Step 3: 运行测试

```bash
dotnet test --filter "CmdSendFileBlockTests"
```

#### Step 4: 使用命令

```csharp
var cmd = new CmdSendFileBlock
{
    Offset = 0,
    Data = fileData
};

var response = await rcspProtocol.SendCommandAsync<RspFileBlock>(cmd);
```

---

## 🧪 测试策略

### 单元测试（推荐）

**测试内容**:
- ✅ 协议序列化/反序列化
- ✅ 数据包解析逻辑
- ✅ 命令创建
- ✅ 状态转换

**运行方式**:
```bash
# 运行所有测试
dotnet test

# 运行特定测试类
dotnet test --filter "RcspPacketTests"

# 运行特定测试方法
dotnet test --filter "ToBytes_ShouldGenerateCorrectFormat"
```

### 集成测试（待实现）

**测试内容**:
- BLE 连接和通信
- 完整 OTA 升级流程
- 设备回连逻辑

**注意**:
- 需要真实蓝牙设备
- 可使用模拟器/桩对象

---

## 📚 参考资料

### 内部文档

1. **架构设计** - `docs/OTA迁移计划.md`
2. **数据结构** - `docs/OTA数据结构设计.md`
3. **实现指南** - `docs/OTA实现指南.md`
4. **故障排查** - `docs/OTA故障排查指南.md`
5. **项目状态** - `PROJECT_STATUS.md`

### 外部资源

- [杰理官方文档](https://doc.zh-jieli.com/vue/#/docs/ota)
- [Avalonia UI 文档](https://docs.avaloniaui.net/)
- [Windows BLE API](https://learn.microsoft.com/en-us/windows/uwp/devices-sensors/bluetooth-low-energy-overview)

### 原始参考代码

**小程序实现**: `WeChat-Mini-Program-OTA/`
- `libs/jl_ota_2.1.1.js` - OTA 核心逻辑
- `libs/jl_rcsp_ota_2.1.1.js` - RCSP 协议
- `code/JLOTA/miniprogram/lib/otaWrapper.ts` - 封装实现

**注意**: 仅供参考思路，不要直接翻译！

---

## 💻 IDE 配置

### Visual Studio 2022

1. 打开 `JieLi.OTA.sln`
2. 设置启动项目：右键 `JieLi.OTA.Desktop` → 设为启动项目
3. 安装推荐扩展：
   - Avalonia for Visual Studio
   - GitHub Copilot

### JetBrains Rider

1. 打开 `JieLi.OTA.sln`
2. 安装 Avalonia XAML 插件
3. 启用 GitHub Copilot

### VS Code

1. 安装扩展：
   - C# Dev Kit
   - Avalonia for VS Code
2. 打开工作区文件夹
3. 使用 `Ctrl+Shift+B` 构建

---

## 🐛 常见问题

### Q: 编译错误 "XTrace 不存在"

**原因**: 缺少 using 语句

**解决**:
```csharp
using NewLife.Log;
```

### Q: 测试无法发现

**原因**: xUnit 版本问题或测试类未 public

**解决**:
```csharp
public class MyTests  // 确保 public
{
    [Fact]  // 确保有 [Fact] 特性
    public void TestMethod() { }
}
```

### Q: Avalonia 设计器无法加载

**原因**: XAML 语法错误或缺少引用

**解决**:
- 检查 xmlns 声明
- 确保编译成功
- 重启 IDE

---

## 📞 获取帮助

- **技术问题**: 查阅 `docs/OTA故障排查指南.md`
- **开发疑问**: 阅读 `docs/OTA实现指南.md`
- **提交 Bug**: GitHub Issues
- **参与讨论**: GitHub Discussions

---

**文档版本**: v1.0  
**最后更新**: 2025-11-04  
**维护人**: PeiKeSmart Team

祝开发愉快！🎉
