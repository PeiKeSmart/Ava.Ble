# JieLi OTA 开发者指南

## 📚 目录

- [开发环境设置](#开发环境设置)
- [项目结构](#项目结构)
- [编码规范](#编码规范)
- [构建与测试](#构建与测试)
- [调试技巧](#调试技巧)
- [贡献指南](#贡献指南)
- [发布流程](#发布流程)

---

## 开发环境设置

### 必需工具

1. **.NET 9.0 SDK**
   - 下载: <https://dotnet.microsoft.com/download/dotnet/9.0>
   - 验证安装: `dotnet --version`

2. **IDE (选择其一)**
   - **Visual Studio 2022** (17.8+)
     - 工作负载: .NET 桌面开发
     - 组件: .NET 9.0 SDK
   - **JetBrains Rider** (2024.1+)
     - 支持 .NET 9.0
   - **Visual Studio Code**
     - 扩展: C# Dev Kit

3. **Git**
   - 下载: <https://git-scm.com/>
   - 配置用户信息:
     ```bash
     git config --global user.name "Your Name"
     git config --global user.email "your.email@example.com"
     ```

### 可选工具

- **Windows Terminal** - 现代化终端
- **GitHub CLI** - GitHub 命令行工具
- **dotnet-format** - 代码格式化
  ```bash
  dotnet tool install -g dotnet-format
  ```

### 克隆项目

```bash
# 克隆仓库
git clone https://github.com/PeiKeSmart/Ava.Ble.git
cd Ava.Ble

# 切换到开发分支
git checkout -b feature/your-feature-name
```

### 还原依赖

```bash
# 还原 NuGet 包
dotnet restore JieLi.OTA.sln

# 验证构建
dotnet build JieLi.OTA.sln
```

---

## 项目结构

### 解决方案组织

```
JieLi.OTA.sln                     # 主解决方案
├── src/                          # 源代码
│   ├── JieLi.OTA.Core/          # 核心层
│   ├── JieLi.OTA.Infrastructure/ # 基础设施层
│   ├── JieLi.OTA.Application/   # 应用层
│   └── JieLi.OTA.Desktop/       # 桌面层
├── tests/                        # 测试项目
│   ├── JieLi.OTA.Core.Tests/
│   ├── JieLi.OTA.Infrastructure.Tests/
│   └── JieLi.OTA.Application.Tests/
└── docs/                         # 文档
```

### 项目依赖关系

```
Desktop → Application → Infrastructure → Core
  ↓           ↓              ↓            ↓
Tests     Tests          Tests        Tests
```

**依赖原则**:
- 上层依赖下层
- 同层之间不能相互依赖
- Core 层不依赖任何项目

### 命名约定

| 类型 | 命名规则 | 示例 |
|------|---------|------|
| 命名空间 | PascalCase | `JieLi.OTA.Core.Protocols` |
| 类 | PascalCase | `RcspPacket`, `OtaManager` |
| 接口 | I + PascalCase | `IBluetoothService` |
| 方法 | PascalCase | `StartOtaAsync` |
| 属性 | PascalCase | `DeviceName`, `IsConnected` |
| 字段 (私有) | _camelCase | `_bluetoothService` |
| 常量 | PascalCase | `MaxRetryCount` |
| 枚举 | PascalCase | `OtaState` |
| 枚举值 | PascalCase | `Connecting`, `Completed` |

---

## 编码规范

### C# 编码标准

项目遵循 [PeiKeSmart Copilot 协作指令](../.github/copilot-instructions.md),主要规范:

#### 1. 使用最新 C# 语法

```csharp
// ✅ 推荐: File-scoped namespace
namespace JieLi.OTA.Core.Protocols;

public class RcspPacket
{
    // ✅ 推荐: 目标类型 new
    public Byte[] Payload { get; set; } = [];
    
    // ✅ 推荐: Pattern matching
    public Boolean IsValid => OpCode switch
    {
        >= 0x00 and <= 0x04 => true,
        _ => false
    };
}

// ❌ 避免: 传统 namespace
namespace JieLi.OTA.Core.Protocols
{
    public class RcspPacket
    {
        // ❌ 避免: 显式类型
        public Byte[] Payload { get; set; } = new Byte[0];
        
        // ❌ 避免: 冗长的 if-else
        public Boolean IsValid
        {
            get
            {
                if (OpCode >= 0x00 && OpCode <= 0x04)
                    return true;
                return false;
            }
        }
    }
}
```

#### 2. 异步方法命名

```csharp
// ✅ 推荐: Async 后缀
public async Task<Boolean> ConnectAsync(String deviceId);
public async Task<RcspPacket> SendCommandAsync(RcspCommand cmd);

// ❌ 避免: 缺少 Async 后缀
public async Task<Boolean> Connect(String deviceId);
```

#### 3. 空值处理

```csharp
// ✅ 推荐: 启用 nullable 引用类型
#nullable enable

public class BleDevice
{
    public String DeviceName { get; set; } // 不可为 null
    public String? NickName { get; set; }  // 可以为 null
}

// ✅ 推荐: 参数验证
public void ProcessDevice(BleDevice device)
{
    ArgumentNullException.ThrowIfNull(device);
    // ...
}
```

#### 4. 字段与属性

```csharp
public class OtaManager
{
    // ✅ 字段紧邻其对应的属性
    private readonly IBluetoothService _bluetoothService;
    public IBluetoothService BluetoothService => _bluetoothService;
    
    private OtaState _currentState;
    public OtaState CurrentState
    {
        get => _currentState;
        private set
        {
            _currentState = value;
            StateChanged?.Invoke(this, value);
        }
    }
}
```

#### 5. 异常处理

```csharp
// ✅ 推荐: 精准异常类型
try
{
    await ConnectAsync(deviceId);
}
catch (TimeoutException ex)
{
    XTrace.WriteLine($"连接超时: {ex.Message}");
}
catch (BluetoothException ex)
{
    XTrace.WriteLine($"蓝牙错误: {ex.Message}");
}

// ❌ 避免: 捕获所有异常
catch (Exception ex)
{
    // 过于宽泛
}
```

#### 6. XML 文档注释

```csharp
/// <summary>开始 OTA 升级</summary>
/// <param name="deviceId">目标设备 ID</param>
/// <param name="firmwareFilePath">固件文件路径</param>
/// <param name="cancellationToken">取消令牌</param>
/// <returns>升级结果</returns>
/// <exception cref="ArgumentNullException">参数为 null</exception>
/// <exception cref="FileNotFoundException">固件文件不存在</exception>
/// <remarks>
/// 升级流程:
/// 1. 连接设备
/// 2. 验证固件
/// 3. 传输文件
/// 4. 重启设备
/// </remarks>
public async Task<OtaResult> StartOtaAsync(
    String deviceId,
    String firmwareFilePath,
    CancellationToken cancellationToken = default)
{
    // ...
}
```

---

## 构建与测试

### 构建项目

```bash
# 完整构建
dotnet build JieLi.OTA.sln -c Debug

# Release 构建
dotnet build JieLi.OTA.sln -c Release

# 清理构建
dotnet clean JieLi.OTA.sln
```

### 运行测试

```bash
# 运行所有测试
dotnet test JieLi.OTA.sln

# 运行特定测试项目
dotnet test tests/JieLi.OTA.Core.Tests

# 带详细输出
dotnet test JieLi.OTA.sln -v normal

# 收集代码覆盖率
dotnet test JieLi.OTA.sln --collect:"XPlat Code Coverage"
```

### 运行应用

```bash
# 调试模式运行
dotnet run --project src/JieLi.OTA.Desktop/JieLi.OTA.Desktop.csproj

# 发布并运行
dotnet publish src/JieLi.OTA.Desktop/JieLi.OTA.Desktop.csproj -c Release
cd src/JieLi.OTA.Desktop/bin/Release/net9.0-windows10.0.19041.0/publish
./JieLi.OTA.Desktop.exe
```

### 代码格式化

```bash
# 格式化所有代码
dotnet format JieLi.OTA.sln

# 仅检查不修改
dotnet format JieLi.OTA.sln --verify-no-changes

# 格式化特定项目
dotnet format src/JieLi.OTA.Core/JieLi.OTA.Core.csproj
```

---

## 调试技巧

### Visual Studio 调试

1. **断点调试**
   - `F9` 设置/取消断点
   - `F5` 开始调试
   - `F10` 单步跳过
   - `F11` 单步进入

2. **条件断点**
   - 右键断点 → 条件
   - 示例: `deviceId == "特定设备ID"`

3. **即时窗口**
   - 调试时按 `Ctrl+Alt+I`
   - 执行表达式: `?device.DeviceName`

### 日志调试

```csharp
using NewLife.Log;

// 启用详细日志
XTrace.UseConsole();
XTrace.Log.Level = LogLevel.Debug;

// 记录日志
XTrace.WriteLine($"设备连接: {deviceId}");
XTrace.WriteException(ex);
```

### 蓝牙调试

**Windows 蓝牙日志**:

1. 启用蓝牙日志:
   ```powershell
   # 以管理员运行
   logman start bth_hci -ets -o bluetooth.etl -p {8a1f9517-3a8c-4a9e-a018-4f17a200f277} 0xFFFFFFFF 0xFF
   ```

2. 重现问题

3. 停止日志:
   ```powershell
   logman stop bth_hci -ets
   ```

4. 使用 Microsoft Message Analyzer 分析 `bluetooth.etl`

---

## 贡献指南

### 工作流程

1. **Fork 仓库**
   - 访问 <https://github.com/PeiKeSmart/Ava.Ble>
   - 点击右上角 "Fork"

2. **创建分支**
   ```bash
   git checkout -b feature/amazing-feature
   ```

3. **提交更改**
   ```bash
   git add .
   git commit -m "Add some amazing feature"
   ```

4. **推送分支**
   ```bash
   git push origin feature/amazing-feature
   ```

5. **创建 Pull Request**
   - 访问你的 Fork 页面
   - 点击 "New Pull Request"

### 提交规范

遵循 [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

<body>

<footer>
```

**类型 (type)**:

- `feat`: 新功能
- `fix`: 修复 bug
- `docs`: 文档更新
- `style`: 代码格式调整
- `refactor`: 重构
- `test`: 测试相关
- `chore`: 构建/工具相关

**示例**:

```
feat(bluetooth): 添加设备类型识别功能

- 根据制造商数据识别设备类型
- 根据服务 UUID 推断设备类型
- 支持 Apple, Google, Samsung 等主流品牌

Closes #123
```

### 代码审查清单

提交 PR 前请确认:

- ✅ 代码符合项目编码规范
- ✅ 所有单元测试通过
- ✅ 添加了必要的单元测试
- ✅ 更新了相关文档
- ✅ 提交信息清晰明了
- ✅ 无不必要的调试代码
- ✅ 无敏感信息 (密钥、密码)

---

## 发布流程

### 版本号规则

遵循 [Semantic Versioning](https://semver.org/):

- `MAJOR.MINOR.PATCH`
- 示例: `1.2.3`

**版本递增规则**:

- `MAJOR`: 不兼容的 API 变更
- `MINOR`: 向后兼容的新功能
- `PATCH`: 向后兼容的 bug 修复

### 发布步骤

#### 1. 更新版本号

编辑 `src/JieLi.OTA.Desktop/JieLi.OTA.Desktop.csproj`:

```xml
<PropertyGroup>
  <Version>1.0.1</Version>
  <AssemblyVersion>1.0.1.0</AssemblyVersion>
  <FileVersion>1.0.1.0</FileVersion>
</PropertyGroup>
```

#### 2. 更新 CHANGELOG

编辑 `CHANGELOG.md`:

```markdown
## [1.0.1] - 2025-11-05

### Added
- 设备类型识别功能

### Fixed
- 修复单备份回连失败问题

### Changed
- 优化传输速度计算逻辑
```

#### 3. 创建发布构建

```bash
# 发布自包含版本 (Windows x64)
dotnet publish src/JieLi.OTA.Desktop/JieLi.OTA.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o publish/win-x64

# 创建 ZIP 包
Compress-Archive -Path publish/win-x64/* -DestinationPath JieLi.OTA.v1.0.1-win-x64.zip
```

#### 4. 创建 Git 标签

```bash
git tag -a v1.0.1 -m "Release version 1.0.1"
git push origin v1.0.1
```

#### 5. 创建 GitHub Release

1. 访问 <https://github.com/PeiKeSmart/Ava.Ble/releases/new>
2. 选择标签: `v1.0.1`
3. 填写标题和说明
4. 上传构建文件
5. 发布

### 自动化发布 (CI/CD)

项目使用 GitHub Actions 自动化发布,参见 `.github/workflows/release.yml`。

---

## 附录

### 常用命令速查

```bash
# 创建新类
dotnet new class -n MyClass -o src/JieLi.OTA.Core/Models

# 添加 NuGet 包
dotnet add src/JieLi.OTA.Core package Newtonsoft.Json

# 列出项目依赖
dotnet list package

# 更新包
dotnet add package Avalonia --version 11.3.0

# 移除包
dotnet remove package Newtonsoft.Json
```

### 有用的资源

- **Avalonia 文档**: <https://docs.avaloniaui.net/>
- **C# 编程指南**: <https://docs.microsoft.com/zh-cn/dotnet/csharp/>
- **xUnit 文档**: <https://xunit.net/>
- **Moq 文档**: <https://github.com/moq/moq4/wiki/Quickstart>
- **杰理 OTA 文档**: <https://doc.zh-jieli.com/vue/#/docs/ota>

### 社区与支持

- **GitHub Issues**: <https://github.com/PeiKeSmart/Ava.Ble/issues>
- **GitHub Discussions**: <https://github.com/PeiKeSmart/Ava.Ble/discussions>
- **Email**: support@peikesmart.com

---

**文档版本**: v1.0  
**最后更新**: 2025-11-04  
**维护者**: PeiKeSmart Team
