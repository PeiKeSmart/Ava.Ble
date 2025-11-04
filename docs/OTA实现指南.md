# OTA实现指南

## 一、开发环境准备

### 1.1 必需组件

- **开发工具**: Visual Studio 2022 或 Rider
- **.NET SDK**: .NET 9.0
- **Windows SDK**: Windows 10 SDK (10.0.19041.0 或更高)
- **测试设备**: 支持BLE的Windows 10/11电脑 + 杰理蓝牙设备

### 1.2 项目结构

```
Avalonia.Ble/
├── Protocols/                    # 协议层
│   ├── Rcsp/                    # RCSP协议
│   │   ├── RcspPacket.cs
│   │   ├── RcspCommand.cs
│   │   ├── RcspResponse.cs
│   │   ├── RcspParser.cs
│   │   └── Commands/            # 命令定义
│   │       ├── CmdGetTargetInfo.cs
│   │       ├── CmdReadFileOffset.cs
│   │       ├── CmdInquireCanUpdate.cs
│   │       ├── CmdEnterUpdateMode.cs
│   │       └── ...
│   └── Auth/                    # 认证协议
│       └── AuthService.cs
├── Services/                    # 服务层
│   ├── BleService.cs           # BLE服务（已有，需扩展）
│   ├── OtaManager.cs           # OTA管理器
│   ├── RcspProtocol.cs         # RCSP协议实现
│   ├── ReconnectService.cs     # 回连服务
│   └── OtaFileService.cs       # 文件服务
├── ViewModels/                 # ViewModel层
│   ├── OtaViewModel.cs         # OTA升级VM
│   └── OtaDeviceViewModel.cs   # 设备选择VM
└── Views/                      # 视图层
    ├── OtaUpgradePage.axaml    # OTA升级页面
    └── OtaDeviceSelectPage.axaml # 设备选择页面
```

---

## 二、阶段1：协议层实现

### 2.1 Step 1: 创建基础协议类

#### 创建 RcspPacket.cs

```csharp
namespace Avalonia.Ble.Protocols.Rcsp;

/// <summary>RCSP数据包</summary>
public class RcspPacket
{
    // 见 docs/OTA数据结构设计.md
}
```

**测试要点**:
- 数据包序列化测试
- 数据包反序列化测试
- 边界条件测试（空数据、最小/最大长度）

#### 创建 RcspParser.cs

```csharp
/// <summary>RCSP数据解析器</summary>
public class RcspParser
{
    private readonly List<byte> _buffer = [];
    
    /// <summary>添加接收到的数据</summary>
    public void AddData(byte[] data)
    {
        _buffer.AddRange(data);
    }
    
    /// <summary>尝试解析完整数据包</summary>
    public RcspPacket? TryParse()
    {
        // 查找帧头
        int headIndex = FindHead();
        if (headIndex < 0)
        {
            // 未找到帧头，清空缓冲区
            _buffer.Clear();
            return null;
        }
        
        // 移除帧头前的无效数据
        if (headIndex > 0)
        {
            _buffer.RemoveRange(0, headIndex);
        }
        
        // 至少需要6个字节（HEAD+FLAG+SN+OpCode+END）
        if (_buffer.Count < 6)
            return null;
        
        // 查找帧尾
        int endIndex = _buffer.IndexOf(RcspPacket.RCSP_END, 5);
        if (endIndex < 0)
        {
            // 未找到帧尾，等待更多数据
            // 但如果缓冲区过大，可能数据错误
            if (_buffer.Count > 1024)
            {
                _buffer.RemoveRange(0, 2); // 移除当前帧头，继续查找
            }
            return null;
        }
        
        // 提取完整数据包
        byte[] packetData = _buffer.GetRange(0, endIndex + 1).ToArray();
        _buffer.RemoveRange(0, endIndex + 1);
        
        // 解析数据包
        return RcspPacket.Parse(packetData);
    }
    
    /// <summary>查找帧头位置</summary>
    private int FindHead()
    {
        for (int i = 0; i < _buffer.Count - 1; i++)
        {
            if (_buffer[i] == RcspPacket.RCSP_HEAD[0] && 
                _buffer[i + 1] == RcspPacket.RCSP_HEAD[1])
            {
                return i;
            }
        }
        return -1;
    }
    
    /// <summary>清空缓冲区</summary>
    public void Clear()
    {
        _buffer.Clear();
    }
}
```

**测试要点**:
- 单个完整包解析
- 多个包连续解析
- 包含无效数据的解析
- 不完整包处理

### 2.2 Step 2: 实现OTA命令

#### 创建命令工厂

```csharp
/// <summary>RCSP命令工厂</summary>
public static class RcspCommandFactory
{
    private static byte _snCounter = 0;
    
    /// <summary>生成序列号</summary>
    public static byte GenerateSn()
    {
        return ++_snCounter;
    }
    
    /// <summary>创建命令</summary>
    public static T CreateCommand<T>() where T : RcspCommand, new()
    {
        var cmd = new T
        {
            Sn = GenerateSn()
        };
        return cmd;
    }
}
```

#### 实现各个OTA命令

按照 `docs/OTA数据结构设计.md` 中的定义实现各个命令类。

**开发顺序建议**:
1. `CmdGetTargetInfo` - 最基础，用于测试RCSP通信
2. `CmdReadFileOffset` - 简单命令，无参数
3. `CmdNotifyFileSize` - 带参数命令
4. `CmdInquireCanUpdate` - 带数据命令
5. 其他命令

### 2.3 Step 3: 实现认证服务（如需要）

```csharp
/// <summary>设备认证服务</summary>
public class AuthService
{
    private readonly IBleService _bleService;
    
    public AuthService(IBleService bleService)
    {
        _bleService = bleService;
    }
    
    /// <summary>开始认证</summary>
    public async Task<bool> StartAuthAsync(BleDeviceInfo device)
    {
        // 根据小程序 jl_auth_2.0.0.js 实现认证逻辑
        // 1. 发送认证请求
        // 2. 接收认证挑战
        // 3. 计算认证响应
        // 4. 发送认证响应
        // 5. 等待认证结果
        
        return false; // 待实现
    }
}
```

**注意**: 如果设备未开启认证，此步骤可以暂时跳过。

---

## 三、阶段2：BLE通信集成

### 3.1 扩展 BleService

在现有的 `BleService.cs` 中添加以下方法：

```csharp
public partial class BleService
{
    // 添加私有字段
    private GattCharacteristic? _writeCharacteristic;
    private GattCharacteristic? _notifyCharacteristic;
    
    /// <summary>数据接收事件</summary>
    public event EventHandler<byte[]>? DataReceived;
    
    /// <summary>连接设备并订阅通知</summary>
    public async Task<bool> ConnectAndSubscribeAsync(BleDeviceInfo deviceInfo)
    {
        try
        {
            // 1. 连接设备
            if (!await ConnectToDeviceAsync(deviceInfo))
                return false;
            
            // 2. 查找RCSP服务和特征值
            var device = await BluetoothLEDevice.FromBluetoothAddressAsync(deviceInfo.Address);
            if (device == null)
                return false;
            
            var servicesResult = await device.GetGattServicesAsync();
            if (servicesResult.Status != GattCommunicationStatus.Success)
                return false;
            
            // 3. 查找特征值（根据实际设备的UUID调整）
            foreach (var service in servicesResult.Services)
            {
                var characteristicsResult = await service.GetCharacteristicsAsync();
                if (characteristicsResult.Status != GattCommunicationStatus.Success)
                    continue;
                
                foreach (var characteristic in characteristicsResult.Characteristics)
                {
                    // 查找写特征值
                    if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write) ||
                        characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse))
                    {
                        _writeCharacteristic = characteristic;
                    }
                    
                    // 查找通知特征值
                    if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
                    {
                        _notifyCharacteristic = characteristic;
                    }
                }
            }
            
            if (_writeCharacteristic == null || _notifyCharacteristic == null)
            {
                ErrorOccurred?.Invoke(this, "未找到RCSP特征值");
                return false;
            }
            
            // 4. 订阅通知
            var cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;
            var status = await _notifyCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);
            
            if (status != GattCommunicationStatus.Success)
            {
                ErrorOccurred?.Invoke(this, "订阅通知失败");
                return false;
            }
            
            // 5. 注册通知处理
            _notifyCharacteristic.ValueChanged += OnCharacteristicValueChanged;
            
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"连接失败: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>写入数据</summary>
    public async Task<bool> WriteDataAsync(byte[] data)
    {
        if (_writeCharacteristic == null)
            return false;
        
        try
        {
            var writer = new Windows.Storage.Streams.DataWriter();
            writer.WriteBytes(data);
            
            var result = await _writeCharacteristic.WriteValueAsync(writer.DetachBuffer());
            return result == GattCommunicationStatus.Success;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"写入数据失败: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>特征值变化处理</summary>
    private void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        var reader = Windows.Storage.Streams.DataReader.FromBuffer(args.CharacteristicValue);
        byte[] data = new byte[reader.UnconsumedBufferLength];
        reader.ReadBytes(data);
        
        DataReceived?.Invoke(this, data);
    }
}
```

**测试要点**:
- 连接并订阅成功
- 能接收到设备数据
- 能成功写入数据
- 异常处理

### 3.2 实现 RcspDataHandler

```csharp
/// <summary>RCSP数据处理器</summary>
public class RcspDataHandler
{
    private readonly IBleService _bleService;
    private readonly RcspParser _parser = new();
    private readonly Dictionary<byte, TaskCompletionSource<RcspResponse>> _pendingCommands = [];
    private readonly object _lockObj = new();
    
    public event EventHandler<RcspCommand>? CommandReceived;
    public event EventHandler<RcspResponse>? ResponseReceived;
    
    public RcspDataHandler(IBleService bleService)
    {
        _bleService = bleService;
        _bleService.DataReceived += OnDataReceived;
    }
    
    /// <summary>发送命令并等待响应</summary>
    public async Task<TResponse> SendCommandAsync<TResponse>(RcspCommand command, int timeoutMs = 5000)
        where TResponse : RcspResponse, new()
    {
        var tcs = new TaskCompletionSource<RcspResponse>();
        
        lock (_lockObj)
        {
            _pendingCommands[command.Sn] = tcs;
        }
        
        // 发送命令
        var packet = command.ToPacket();
        var data = packet.ToBytes();
        
        if (!await _bleService.WriteDataAsync(data))
        {
            lock (_lockObj)
            {
                _pendingCommands.Remove(command.Sn);
            }
            throw new IOException("发送命令失败");
        }
        
        // 等待响应（带超时）
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            var response = await tcs.Task.WaitAsync(cts.Token);
            return (TResponse)response;
        }
        catch (OperationCanceledException)
        {
            lock (_lockObj)
            {
                _pendingCommands.Remove(command.Sn);
            }
            throw new TimeoutException("等待响应超时");
        }
    }
    
    /// <summary>接收数据处理</summary>
    private void OnDataReceived(object? sender, byte[] data)
    {
        _parser.AddData(data);
        
        while (true)
        {
            var packet = _parser.TryParse();
            if (packet == null)
                break;
            
            if (packet.IsCommand)
            {
                // 处理设备主动发送的命令
                var command = ParseCommand(packet);
                if (command != null)
                {
                    CommandReceived?.Invoke(this, command);
                }
            }
            else
            {
                // 处理响应
                var response = ParseResponse(packet);
                if (response != null)
                {
                    ResponseReceived?.Invoke(this, response);
                    
                    // 完成等待的任务
                    lock (_lockObj)
                    {
                        if (_pendingCommands.TryGetValue(packet.Sn, out var tcs))
                        {
                            _pendingCommands.Remove(packet.Sn);
                            tcs.SetResult(response);
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>解析命令</summary>
    private RcspCommand? ParseCommand(RcspPacket packet)
    {
        // 根据OpCode创建相应的命令对象
        return packet.OpCode switch
        {
            OtaOpCode.CMD_OTA_SEND_FILE_BLOCK => new CmdRequestFileBlock(),
            // ... 其他命令
            _ => null
        };
    }
    
    /// <summary>解析响应</summary>
    private RcspResponse? ParseResponse(RcspPacket packet)
    {
        RcspResponse response = packet.OpCode switch
        {
            OtaOpCode.CMD_GET_TARGET_INFO => new RspDeviceInfo(),
            OtaOpCode.CMD_OTA_GET_FILE_OFFSET => new RspFileOffset(),
            OtaOpCode.CMD_OTA_INQUIRE_CAN_UPDATE => new RspCanUpdate(),
            OtaOpCode.CMD_OTA_ENTER_UPDATE_MODE => new RspEnterUpdateMode(),
            OtaOpCode.CMD_OTA_QUERY_UPDATE_RESULT => new RspUpdateResult(),
            // ... 其他响应
            _ => new RcspResponse()
        };
        
        response.ParseFromPacket(packet);
        return response;
    }
}
```

---

## 四、阶段3：OTA业务逻辑

### 4.1 实现 RcspProtocol

```csharp
/// <summary>RCSP协议实现</summary>
public class RcspProtocol : IRcspProtocol
{
    private readonly RcspDataHandler _dataHandler;
    private RspDeviceInfo? _deviceInfo;
    
    public bool IsDeviceConnected { get; private set; }
    public RspDeviceInfo? CurrentDeviceInfo => _deviceInfo;
    
    public RcspProtocol(IBleService bleService)
    {
        _dataHandler = new RcspDataHandler(bleService);
    }
    
    /// <summary>初始化RCSP</summary>
    public async Task<RspDeviceInfo> InitializeAsync(BleDeviceInfo device)
    {
        // 1. 获取设备信息
        var cmd = RcspCommandFactory.CreateCommand<CmdGetTargetInfo>();
        _deviceInfo = await _dataHandler.SendCommandAsync<RspDeviceInfo>(cmd);
        
        if (_deviceInfo == null || !_deviceInfo.IsSuccess)
            throw new Exception("获取设备信息失败");
        
        IsDeviceConnected = true;
        return _deviceInfo;
    }
    
    /// <summary>发送命令</summary>
    public Task<TResponse> SendCommandAsync<TResponse>(RcspCommand command, int timeoutMs = 5000)
        where TResponse : RcspResponse, new()
    {
        return _dataHandler.SendCommandAsync<TResponse>(command, timeoutMs);
    }
    
    public void RegisterCallback(IRcspCallback callback)
    {
        // 实现回调注册
    }
    
    public void UnregisterCallback(IRcspCallback callback)
    {
        // 实现回调注销
    }
    
    public void Release()
    {
        IsDeviceConnected = false;
        _deviceInfo = null;
    }
}
```

### 4.2 实现 OtaFileService

```csharp
/// <summary>OTA文件服务</summary>
public class OtaFileService
{
    /// <summary>验证升级文件</summary>
    public bool ValidateFile(string filePath)
    {
        if (!File.Exists(filePath))
            return false;
        
        // 检查文件扩展名
        var ext = Path.GetExtension(filePath).ToLower();
        if (ext != ".ufw" && ext != ".bin")
            return false;
        
        // 检查文件大小
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0 || fileInfo.Length > 10 * 1024 * 1024) // 最大10MB
            return false;
        
        return true;
    }
    
    /// <summary>读取文件数据</summary>
    public byte[] ReadFile(string filePath)
    {
        return File.ReadAllBytes(filePath);
    }
    
    /// <summary>读取文件块</summary>
    public byte[] ReadFileBlock(byte[] fileData, int offset, int length)
    {
        if (offset < 0 || offset >= fileData.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        
        int actualLength = Math.Min(length, fileData.Length - offset);
        byte[] block = new byte[actualLength];
        Buffer.BlockCopy(fileData, offset, block, 0, actualLength);
        
        return block;
    }
    
    /// <summary>计算CRC16</summary>
    public ushort CalculateCrc16(byte[] data)
    {
        ushort crc = 0xFFFF;
        
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x0001) != 0)
                {
                    crc >>= 1;
                    crc ^= 0xA001;
                }
                else
                {
                    crc >>= 1;
                }
            }
        }
        
        return crc;
    }
}
```

### 4.3 实现 OtaManager（核心）

```csharp
/// <summary>OTA管理器</summary>
public class OtaManager : IOtaManager
{
    private readonly IRcspProtocol _rcspProtocol;
    private readonly OtaFileService _fileService;
    private OtaState _currentState = OtaState.Idle;
    private IOtaCallback? _callback;
    private byte[]? _fileData;
    private int _transferredBytes;
    
    public OtaState CurrentState => _currentState;
    public bool IsUpgrading => _currentState != OtaState.Idle && 
                               _currentState != OtaState.Success && 
                               _currentState != OtaState.Failed;
    
    public OtaManager(IRcspProtocol rcspProtocol, OtaFileService fileService)
    {
        _rcspProtocol = rcspProtocol;
        _fileService = fileService;
    }
    
    /// <summary>开始OTA升级</summary>
    public async Task StartOtaAsync(BleDeviceInfo device, OtaConfig config, IOtaCallback callback)
    {
        if (IsUpgrading)
            throw new InvalidOperationException("升级正在进行中");
        
        _callback = callback;
        _currentState = OtaState.FileChecking;
        
        try
        {
            // 1. 通知开始
            callback.OnStartOta();
            
            // 2. 校验文件
            if (!await ValidateFileAsync(config))
            {
                throw new Exception("文件校验失败");
            }
            
            // 3. 读取文件偏移
            _currentState = OtaState.ReadingFileOffset;
            var offsetCmd = RcspCommandFactory.CreateCommand<CmdReadFileOffset>();
            var offsetRsp = await _rcspProtocol.SendCommandAsync<RspFileOffset>(offsetCmd);
            
            // 4. 查询可升级
            _currentState = OtaState.InquiringCanUpdate;
            var inquireCmd = RcspCommandFactory.CreateCommand<CmdInquireCanUpdate>();
            inquireCmd.FirmwareData = _fileData.Take(256).ToArray(); // 发送前256字节给设备校验
            var inquireRsp = await _rcspProtocol.SendCommandAsync<RspCanUpdate>(inquireCmd);
            
            if (inquireRsp.Result != RspCanUpdate.RESULT_CAN_UPDATE)
            {
                throw new Exception($"设备无法升级: {GetCanUpdateErrorMessage(inquireRsp.Result)}");
            }
            
            // 5. 通知文件大小
            var notifyCmd = RcspCommandFactory.CreateCommand<CmdNotifyFileSize>();
            notifyCmd.TotalSize = _fileData.Length;
            notifyCmd.CurrentSize = 0;
            await _rcspProtocol.SendCommandAsync<RcspResponse>(notifyCmd);
            
            // 6. 进入升级模式
            _currentState = OtaState.EnteringUpdateMode;
            var enterCmd = RcspCommandFactory.CreateCommand<CmdEnterUpdateMode>();
            var enterRsp = await _rcspProtocol.SendCommandAsync<RspEnterUpdateMode>(enterCmd);
            
            if (!enterRsp.IsSuccess)
            {
                throw new Exception("进入升级模式失败");
            }
            
            // 7. 传输文件
            _currentState = OtaState.Transferring;
            _transferredBytes = 0;
            
            // 注册文件块请求处理
            // （这里需要监听设备主动发送的 CMD_OTA_SEND_FILE_BLOCK 命令）
            // 在 RcspDataHandler 的 CommandReceived 事件中处理
            
            // 等待传输完成...
            
            // 8. 查询升级结果
            _currentState = OtaState.QueryingResult;
            var resultCmd = RcspCommandFactory.CreateCommand<CmdQueryUpdateResult>();
            var resultRsp = await _rcspProtocol.SendCommandAsync<RspUpdateResult>(resultCmd);
            
            if (resultRsp.Result != RspUpdateResult.RESULT_COMPLETE)
            {
                throw new Exception($"升级失败: {GetUpdateResultErrorMessage(resultRsp.Result)}");
            }
            
            // 9. 重启设备
            _currentState = OtaState.Rebooting;
            var rebootCmd = RcspCommandFactory.CreateCommand<CmdRebootDevice>();
            await _rcspProtocol.SendCommandAsync<RcspResponse>(rebootCmd);
            
            // 10. 完成
            _currentState = OtaState.Success;
            callback.OnStopOta();
        }
        catch (Exception ex)
        {
            _currentState = OtaState.Failed;
            callback.OnError(OtaErrorCode.ERROR_OTA_FAIL, ex.Message);
        }
    }
    
    /// <summary>处理设备请求文件块</summary>
    private async Task HandleFileBlockRequest(CmdRequestFileBlock request)
    {
        if (_fileData == null)
            return;
        
        // 读取文件块
        var blockData = _fileService.ReadFileBlock(_fileData, request.Offset, request.Length);
        
        // 创建响应
        var response = RspFileBlock.CreateResponse(request.Sn, blockData);
        
        // 发送响应
        await _rcspProtocol.SendCommandAsync<RcspResponse>(...)
        
        // 更新进度
        _transferredBytes += blockData.Length;
        int percentage = (int)(_transferredBytes * 100.0 / _fileData.Length);
        
        _callback?.OnProgress(new OtaProgress
        {
            Type = UpgradeType.Firmware,
            State = _currentState,
            Percentage = percentage,
            TransferredBytes = _transferredBytes,
            TotalBytes = _fileData.Length
        });
    }
    
    public bool CancelOta()
    {
        // 仅双备份支持取消
        if (_currentState == OtaState.Transferring)
        {
            _currentState = OtaState.Cancelled;
            _callback?.OnCancelOta();
            return true;
        }
        return false;
    }
    
    public void Release()
    {
        _callback = null;
        _fileData = null;
        _currentState = OtaState.Idle;
    }
}
```

**注意**: 这只是核心流程的简化版本，实际实现需要：
- 处理更多异常情况
- 实现回连逻辑（单备份）
- 完善进度计算
- 添加日志记录

---

## 五、阶段4：UI与ViewModel

### 5.1 创建 OtaViewModel

```csharp
public partial class OtaViewModel : ViewModelBase, IOtaCallback
{
    private readonly IOtaManager _otaManager;
    private readonly OtaFileService _fileService;
    
    [ObservableProperty]
    private BleDeviceInfo? _selectedDevice;
    
    [ObservableProperty]
    private string? _selectedFilePath;
    
    [ObservableProperty]
    private OtaState _currentState = OtaState.Idle;
    
    [ObservableProperty]
    private int _progress;
    
    [ObservableProperty]
    private string? _statusMessage;
    
    [ObservableProperty]
    private bool _isUpgrading;
    
    public OtaViewModel(IOtaManager otaManager, OtaFileService fileService)
    {
        _otaManager = otaManager;
        _fileService = fileService;
    }
    
    [RelayCommand]
    private async Task SelectFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择升级文件",
            Filters = new List<FileDialogFilter>
            {
                new() { Name = "升级文件", Extensions = new List<string> { "ufw", "bin" } },
                new() { Name = "所有文件", Extensions = new List<string> { "*" } }
            }
        };
        
        var result = await dialog.ShowAsync(/* parent window */);
        if (result != null && result.Length > 0)
        {
            SelectedFilePath = result[0];
        }
    }
    
    [RelayCommand(CanExecute = nameof(CanStartUpgrade))]
    private async Task StartUpgradeAsync()
    {
        if (SelectedDevice == null || string.IsNullOrEmpty(SelectedFilePath))
            return;
        
        IsUpgrading = true;
        
        var config = new OtaConfig
        {
            UpdateFilePath = SelectedFilePath,
            UpdateFileData = _fileService.ReadFile(SelectedFilePath)
        };
        
        await _otaManager.StartOtaAsync(SelectedDevice, config, this);
    }
    
    private bool CanStartUpgrade()
    {
        return SelectedDevice != null && 
               !string.IsNullOrEmpty(SelectedFilePath) && 
               !IsUpgrading;
    }
    
    [RelayCommand]
    private void CancelUpgrade()
    {
        _otaManager.CancelOta();
    }
    
    // IOtaCallback 实现
    public void OnStartOta()
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = "开始升级...";
            Progress = 0;
        });
    }
    
    public void OnProgress(OtaProgress progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentState = progress.State;
            Progress = progress.Percentage;
            StatusMessage = progress.Message ?? GetStateMessage(progress.State);
        });
    }
    
    public void OnStopOta()
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = "升级成功！";
            IsUpgrading = false;
        });
    }
    
    public void OnError(int errorCode, string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = $"升级失败: {message}";
            IsUpgrading = false;
        });
    }
    
    public void OnNeedReconnect(ReconnectInfo reconnectInfo)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = "等待设备回连...";
        });
    }
    
    public void OnCancelOta()
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = "升级已取消";
            IsUpgrading = false;
        });
    }
    
    private string GetStateMessage(OtaState state)
    {
        return state switch
        {
            OtaState.FileChecking => "校验文件...",
            OtaState.DeviceConnecting => "连接设备...",
            OtaState.RcspInitializing => "初始化通信...",
            OtaState.ReadingFileOffset => "读取设备状态...",
            OtaState.InquiringCanUpdate => "检查升级条件...",
            OtaState.EnteringUpdateMode => "进入升级模式...",
            OtaState.Transferring => $"传输中... {Progress}%",
            OtaState.QueryingResult => "查询升级结果...",
            OtaState.Rebooting => "重启设备...",
            _ => ""
        };
    }
}
```

### 5.2 创建 UI 页面

```xml
<!-- OtaUpgradePage.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Avalonia.Ble.ViewModels"
             x:Class="Avalonia.Ble.Views.OtaUpgradePage"
             x:DataType="vm:OtaViewModel">
    
    <Grid RowDefinitions="Auto,*,Auto" Margin="20">
        <!-- 设备和文件选择 -->
        <StackPanel Grid.Row="0" Spacing="10">
            <TextBlock Text="选择设备" FontWeight="Bold"/>
            <ComboBox ItemsSource="{Binding Devices}"
                      SelectedItem="{Binding SelectedDevice}"
                      DisplayMemberBinding="{Binding DisplayName}"
                      HorizontalAlignment="Stretch"/>
            
            <TextBlock Text="升级文件" FontWeight="Bold" Margin="0,10,0,0"/>
            <Grid ColumnDefinitions="*,Auto">
                <TextBox Grid.Column="0" 
                         Text="{Binding SelectedFilePath}" 
                         IsReadOnly="True"/>
                <Button Grid.Column="1" 
                        Content="选择文件" 
                        Command="{Binding SelectFileCommand}"
                        Margin="5,0,0,0"/>
            </Grid>
        </StackPanel>
        
        <!-- 升级进度 -->
        <Border Grid.Row="1" 
                BorderBrush="Gray" 
                BorderThickness="1" 
                CornerRadius="5" 
                Padding="20"
                Margin="0,20,0,0">
            <StackPanel Spacing="15">
                <TextBlock Text="{Binding StatusMessage}" 
                           FontSize="16"
                           TextAlignment="Center"/>
                
                <ProgressBar Value="{Binding Progress}" 
                             Maximum="100"
                             Height="30"
                             ShowProgressText="True"/>
                
                <TextBlock Text="{Binding CurrentState}" 
                           HorizontalAlignment="Center"
                           Foreground="Gray"/>
            </StackPanel>
        </Border>
        
        <!-- 操作按钮 -->
        <StackPanel Grid.Row="2" 
                    Orientation="Horizontal" 
                    HorizontalAlignment="Center"
                    Spacing="10"
                    Margin="0,20,0,0">
            <Button Content="开始升级" 
                    Command="{Binding StartUpgradeCommand}"
                    IsEnabled="{Binding !IsUpgrading}"
                    Width="120"
                    Height="40"/>
            
            <Button Content="取消升级" 
                    Command="{Binding CancelUpgradeCommand}"
                    IsEnabled="{Binding IsUpgrading}"
                    Width="120"
                    Height="40"/>
        </StackPanel>
    </Grid>
</UserControl>
```

---

## 六、测试指南

### 6.1 单元测试示例

```csharp
public class RcspPacketTests
{
    [Fact]
    public void ToBytes_ShouldSerializeCorrectly()
    {
        // Arrange
        var packet = new RcspPacket
        {
            OpCode = 0x02,
            Sn = 0x01,
            IsCommand = true,
            NeedResponse = true,
            Payload = [0x01, 0x02, 0x03]
        };
        
        // Act
        var bytes = packet.ToBytes();
        
        // Assert
        Assert.Equal(0xAA, bytes[0]);
        Assert.Equal(0x55, bytes[1]);
        Assert.Equal(0xC0, bytes[2]); // FLAG
        Assert.Equal(0x01, bytes[3]); // SN
        Assert.Equal(0x02, bytes[4]); // OpCode
        Assert.Equal(0xAD, bytes[^1]); // END
    }
    
    [Fact]
    public void Parse_ShouldDeserializeCorrectly()
    {
        // Arrange
        byte[] data = [0xAA, 0x55, 0xC0, 0x01, 0x02, 0x01, 0x02, 0x03, 0xAD];
        
        // Act
        var packet = RcspPacket.Parse(data);
        
        // Assert
        Assert.NotNull(packet);
        Assert.Equal(0x02, packet.OpCode);
        Assert.Equal(0x01, packet.Sn);
        Assert.True(packet.IsCommand);
        Assert.True(packet.NeedResponse);
        Assert.Equal(3, packet.Payload.Length);
    }
}
```

### 6.2 集成测试

```csharp
[Fact]
public async Task FullOtaProcess_ShouldSucceed()
{
    // Arrange
    var bleService = new BleService();
    var rcspProtocol = new RcspProtocol(bleService);
    var fileService = new OtaFileService();
    var otaManager = new OtaManager(rcspProtocol, fileService);
    
    var device = /* 获取测试设备 */;
    var config = new OtaConfig
    {
        UpdateFilePath = "test.ufw"
    };
    
    var callback = new TestOtaCallback();
    
    // Act
    await otaManager.StartOtaAsync(device, config, callback);
    
    // Assert
    Assert.True(callback.IsSuccess);
}
```

---

## 七、调试技巧

### 7.1 日志输出

在关键位置添加日志：

```csharp
XTrace.WriteLine($"[RCSP] 发送命令: OpCode={cmd.OpCode:X2}, SN={cmd.Sn}");
XTrace.WriteLine($"[RCSP] 接收响应: OpCode={rsp.OpCode:X2}, Status={rsp.Status}");
XTrace.WriteLine($"[OTA] 当前状态: {_currentState}, 进度: {_progress}%");
```

### 7.2 数据包查看

创建辅助方法查看数据包：

```csharp
private string BytesToHex(byte[] bytes)
{
    return BitConverter.ToString(bytes).Replace("-", " ");
}

XTrace.WriteLine($"[RAW] 发送: {BytesToHex(data)}");
XTrace.WriteLine($"[RAW] 接收: {BytesToHex(data)}");
```

### 7.3 使用蓝牙调试工具

- **nRF Connect**: 查看BLE广播、连接、特征值
- **Wireshark**: 抓取蓝牙数据包（需要特定硬件）

---

## 八、常见问题

### Q1: 找不到RCSP特征值

**解决**: 使用 nRF Connect 查看设备的服务和特征值UUID，根据实际情况调整代码中的UUID。

### Q2: 数据包解析失败

**解决**: 检查数据包格式，对比小程序代码，确认字节序（大端/小端）。

### Q3: 回连超时

**解决**: 增加回连超时时间，检查MAC地址匹配逻辑。

### Q4: 传输速度慢

**解决**: 
- 协商更大的MTU
- 减少发送间隔
- 使用 WriteWithoutResponse 特征

---

**文档版本**: v1.0  
**创建日期**: 2025-11-04  
**维护人**: PeiKeSmart Team
