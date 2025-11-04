# OTA故障排查指南

## 一、连接问题

### 1.1 无法发现设备

**症状**: 扫描不到目标设备

**可能原因**:
- 设备未开启或处于非广播状态
- 设备已被其他应用连接
- 蓝牙适配器未启用
- Windows蓝牙权限未授予

**排查步骤**:

1. **检查设备状态**
   - 确认设备已开机且处于可连接状态
   - 查看设备指示灯是否正常

2. **检查Windows蓝牙**
   ```powershell
   # 检查蓝牙服务状态
   Get-Service bthserv
   
   # 如果未运行，启动服务
   Start-Service bthserv
   ```

3. **检查应用权限**
   - 打开 设置 > 隐私和安全性 > 蓝牙
   - 确认应用有蓝牙访问权限

4. **使用nRF Connect验证**
   - 安装nRF Connect for Desktop
   - 查看能否扫描到设备
   - 记录设备的广播数据和服务UUID

### 1.2 连接失败

**症状**: 扫描到设备但连接失败

**可能原因**:
- 设备距离过远，信号弱
- 设备已达到最大连接数
- BLE栈异常
- 配对信息冲突

**排查步骤**:

1. **检查信号强度**
   ```csharp
   XTrace.WriteLine($"设备RSSI: {device.Rssi} dBm");
   // RSSI > -70 为良好，-70 ~ -85 为中等，< -85 为弱
   ```

2. **清除配对信息**
   - 打开 设置 > 蓝牙和其他设备
   - 找到目标设备，点击"删除设备"
   - 重新扫描连接

3. **重置蓝牙适配器**
   ```powershell
   # 禁用蓝牙适配器
   Disable-NetAdapter -Name "Bluetooth"
   Start-Sleep -Seconds 2
   # 启用蓝牙适配器
   Enable-NetAdapter -Name "Bluetooth"
   ```

4. **检查连接超时**
   ```csharp
   // 增加连接超时时间
   var connectTask = device.ConnectAsync();
   if (await Task.WhenAny(connectTask, Task.Delay(30000)) != connectTask)
   {
       XTrace.WriteLine("连接超时");
   }
   ```

### 1.3 找不到RCSP服务或特征值

**症状**: 连接成功但找不到所需的服务或特征值

**可能原因**:
- UUID不匹配
- 设备处于特殊模式
- 服务未启用

**排查步骤**:

1. **列出所有服务和特征值**
   ```csharp
   var servicesResult = await device.GetGattServicesAsync();
   foreach (var service in servicesResult.Services)
   {
       XTrace.WriteLine($"服务: {service.Uuid}");
       
       var charsResult = await service.GetCharacteristicsAsync();
       foreach (var characteristic in charsResult.Characteristics)
       {
           XTrace.WriteLine($"  特征值: {characteristic.Uuid}");
           XTrace.WriteLine($"  属性: {characteristic.CharacteristicProperties}");
       }
   }
   ```

2. **对比小程序代码**
   - 查看小程序中使用的服务UUID
   - 查看小程序中使用的特征值UUID
   - 确保UUID匹配（注意大小写和格式）

3. **使用通用UUID**
   ```csharp
   // 如果找不到特定UUID，尝试查找具有所需属性的特征值
   var writeChars = characteristics.Where(c => 
       c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write) ||
       c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse));
   
   var notifyChars = characteristics.Where(c =>
       c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify));
   ```

---

## 二、通信问题

### 2.1 发送数据失败

**症状**: WriteValueAsync 返回失败或超时

**可能原因**:
- MTU过小，数据包过大
- 发送频率过快
- 特征值不支持写入
- 设备缓冲区满

**排查步骤**:

1. **检查MTU**
   ```csharp
   var session = await GattSession.FromDeviceIdAsync(deviceId);
   XTrace.WriteLine($"当前MTU: {session.MaxPduSize}");
   
   // 尝试请求更大的MTU
   session.MaxPduSizeChanged += (s, args) => {
       XTrace.WriteLine($"MTU变更: {args}");
   };
   await session.RequestMaxPduSizeAsync(512);
   ```

2. **分包发送**
   ```csharp
   private async Task SendLargeDataAsync(byte[] data)
   {
       int mtu = _currentMtu - 3; // 减去ATT头部
       int chunkSize = Math.Min(mtu, 20); // 保守起见，使用20字节
       
       for (int i = 0; i < data.Length; i += chunkSize)
       {
           int length = Math.Min(chunkSize, data.Length - i);
           byte[] chunk = new byte[length];
           Buffer.BlockCopy(data, i, chunk, 0, length);
           
           await WriteCharacteristicAsync(chunk);
           await Task.Delay(10); // 添加延迟
       }
   }
   ```

3. **增加重试机制**
   ```csharp
   private async Task<bool> WriteWithRetryAsync(byte[] data, int maxRetries = 3)
   {
       for (int i = 0; i < maxRetries; i++)
       {
           try
           {
               var result = await _writeCharacteristic.WriteValueAsync(buffer);
               if (result == GattCommunicationStatus.Success)
                   return true;
                   
               XTrace.WriteLine($"写入失败，重试 {i + 1}/{maxRetries}");
               await Task.Delay(100);
           }
           catch (Exception ex)
           {
               XTrace.WriteLine($"写入异常: {ex.Message}");
           }
       }
       return false;
   }
   ```

### 2.2 接收数据不完整

**症状**: 接收到的数据包被截断或丢失

**可能原因**:
- 数据包被分片传输
- 缓冲区未正确处理
- 通知事件处理过慢

**排查步骤**:

1. **检查数据包完整性**
   ```csharp
   private void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
   {
       var reader = Windows.Storage.Streams.DataReader.FromBuffer(args.CharacteristicValue);
       byte[] data = new byte[reader.UnconsumedBufferLength];
       reader.ReadBytes(data);
       
       XTrace.WriteLine($"[RX] {data.Length} bytes: {BytesToHex(data)}");
       
       // 添加到解析器
       _parser.AddData(data);
   }
   ```

2. **完善数据包解析器**
   ```csharp
   public class RcspParser
   {
       private readonly List<byte> _buffer = new();
       private const int MAX_BUFFER_SIZE = 2048;
       
       public void AddData(byte[] data)
       {
           _buffer.AddRange(data);
           
           // 防止缓冲区无限增长
           if (_buffer.Count > MAX_BUFFER_SIZE)
           {
               XTrace.WriteLine($"缓冲区溢出，清空: {_buffer.Count} bytes");
               _buffer.Clear();
           }
       }
       
       public RcspPacket? TryParse()
       {
           // 详细的解析逻辑...
       }
   }
   ```

3. **记录原始数据**
   ```csharp
   private readonly List<byte[]> _rawDataLog = new();
   
   private void LogRawData(byte[] data)
   {
       _rawDataLog.Add(data);
       
       // 定期保存到文件
       if (_rawDataLog.Count >= 100)
       {
           SaveRawDataLog();
           _rawDataLog.Clear();
       }
   }
   ```

### 2.3 响应超时

**症状**: SendCommandAsync 抛出 TimeoutException

**可能原因**:
- 设备未响应
- 序列号不匹配
- 响应包格式错误
- 设备繁忙

**排查步骤**:

1. **增加超时时间**
   ```csharp
   // 根据命令类型设置不同的超时时间
   var timeout = cmd.OpCode switch
   {
       OtaOpCode.CMD_OTA_ENTER_UPDATE_MODE => 10000, // 10秒
       OtaOpCode.CMD_OTA_QUERY_UPDATE_RESULT => 15000, // 15秒
       _ => 5000 // 5秒
   };
   
   var response = await SendCommandAsync<TResponse>(cmd, timeout);
   ```

2. **检查序列号匹配**
   ```csharp
   private void OnResponseReceived(RcspResponse response)
   {
       XTrace.WriteLine($"[RSP] OpCode={response.OpCode:X2}, SN={response.Sn}");
       
       lock (_lockObj)
       {
           if (_pendingCommands.TryGetValue(response.Sn, out var tcs))
           {
               _pendingCommands.Remove(response.Sn);
               tcs.SetResult(response);
           }
           else
           {
               XTrace.WriteLine($"[WARN] 未找到匹配的命令: SN={response.Sn}");
           }
       }
   }
   ```

3. **添加命令队列**
   ```csharp
   private readonly SemaphoreSlim _commandSemaphore = new(1, 1);
   
   public async Task<TResponse> SendCommandAsync<TResponse>(RcspCommand command, int timeoutMs)
       where TResponse : RcspResponse, new()
   {
       await _commandSemaphore.WaitAsync();
       
       try
       {
           // 发送命令并等待响应
           return await SendCommandInternalAsync<TResponse>(command, timeoutMs);
       }
       finally
       {
           _commandSemaphore.Release();
       }
   }
   ```

---

## 三、升级问题

### 3.1 设备无法升级

**症状**: 查询可升级返回错误码

**错误码对照**:

| 错误码 | 说明 | 解决方案 |
|--------|------|---------|
| 0x01 | 设备电量低 | 为设备充电或连接电源 |
| 0x02 | 固件信息错误 | 检查升级文件是否匹配设备型号 |
| 0x03 | 版本一致 | 确认升级文件版本高于当前版本 |
| 0x04 | TWS未连接 | 确保TWS耳机双耳都已连接 |
| 0x05 | 耳机不在充电仓 | 将耳机放入充电仓 |

**排查步骤**:

1. **获取设备信息**
   ```csharp
   var deviceInfo = await _rcspProtocol.InitializeAsync(device);
   XTrace.WriteLine($"设备名称: {deviceInfo.DeviceName}");
   XTrace.WriteLine($"固件版本: {deviceInfo.VersionName} ({deviceInfo.VersionCode})");
   XTrace.WriteLine($"电池电量: {deviceInfo.BatteryLevel}%");
   XTrace.WriteLine($"双备份: {deviceInfo.IsSupportDoubleBackup}");
   ```

2. **验证升级文件**
   ```csharp
   // 读取升级文件头部信息
   var fileHeader = _fileService.ReadFileBlock(_fileData, 0, 256);
   XTrace.WriteLine($"文件头: {BytesToHex(fileHeader)}");
   
   // 发送给设备校验
   var inquireCmd = RcspCommandFactory.CreateCommand<CmdInquireCanUpdate>();
   inquireCmd.FirmwareData = fileHeader;
   var inquireRsp = await _rcspProtocol.SendCommandAsync<RspCanUpdate>(inquireCmd);
   
   if (inquireRsp.Result != RspCanUpdate.RESULT_CAN_UPDATE)
   {
       XTrace.WriteLine($"设备拒绝升级: {GetCanUpdateErrorMessage(inquireRsp.Result)}");
   }
   ```

### 3.2 传输中断

**症状**: 升级过程中传输停止

**可能原因**:
- 连接断开
- 设备缓冲区满
- 数据校验失败
- 设备重启

**排查步骤**:

1. **监控连接状态**
   ```csharp
   device.ConnectionStatusChanged += (sender, args) => {
       XTrace.WriteLine($"连接状态变更: {args.Status}");
       
       if (args.Status == BluetoothConnectionStatus.Disconnected)
       {
           // 尝试重连或停止升级
           HandleConnectionLost();
       }
   };
   ```

2. **记录传输进度**
   ```csharp
   private void OnFileBlockResponse(int offset, int length)
   {
       _transferredBytes += length;
       var percentage = (int)(_transferredBytes * 100.0 / _totalBytes);
       
       XTrace.WriteLine($"传输进度: {_transferredBytes}/{_totalBytes} ({percentage}%)");
       
       // 定期保存进度
       if (_transferredBytes % 10240 == 0) // 每10KB保存一次
       {
           SaveProgress(offset, _transferredBytes);
       }
   }
   ```

3. **实现断点续传（双备份）**
   ```csharp
   private async Task ResumeTransferAsync()
   {
       // 读取上次的偏移
       var lastOffset = LoadLastProgress();
       
       XTrace.WriteLine($"从偏移 {lastOffset} 恢复传输");
       
       // 继续传输
       _transferredBytes = lastOffset;
       // ... 继续升级流程
   }
   ```

### 3.3 回连失败（单备份）

**症状**: 设备重启后无法回连

**可能原因**:
- MAC地址匹配错误
- 扫描时间不足
- 设备启动时间过长
- 回连方式不匹配

**排查步骤**:

1. **检查回连信息**
   ```csharp
   public void OnNeedReconnect(ReconnectInfo reconnectInfo)
   {
       XTrace.WriteLine($"需要回连:");
       XTrace.WriteLine($"  支持新回连方式: {reconnectInfo.IsSupportNewReconnectAdv}");
       XTrace.WriteLine($"  设备MAC: {reconnectInfo.DeviceBleMac}");
       XTrace.WriteLine($"  原设备ID: {reconnectInfo.OriginalDeviceId}");
   }
   ```

2. **详细记录扫描设备**
   ```csharp
   private bool IsReconnectDevice(BleDeviceInfo scanDevice, ReconnectInfo reconnectInfo)
   {
       XTrace.WriteLine($"检查设备: {scanDevice.DeviceId}");
       
       if (reconnectInfo.IsSupportNewReconnectAdv)
       {
           // 新回连方式：解析广播包
           var advertisData = ParseAdvertisementData(scanDevice.AdvertisementData);
           var macFromAdv = ExtractMacFromAdvertisement(advertisData);
           
           XTrace.WriteLine($"  广播包MAC: {macFromAdv}");
           XTrace.WriteLine($"  目标MAC: {reconnectInfo.DeviceBleMac}");
           
           return macFromAdv?.Equals(reconnectInfo.DeviceBleMac, StringComparison.OrdinalIgnoreCase) == true;
       }
       else
       {
           // 旧回连方式：匹配Device ID
           bool isMatch = scanDevice.DeviceId.StartsWith(
               reconnectInfo.OriginalDeviceId.Substring(0, 10),
               StringComparison.OrdinalIgnoreCase);
           
           XTrace.WriteLine($"  Device ID匹配: {isMatch}");
           return isMatch;
       }
   }
   ```

3. **增加回连超时和重试**
   ```csharp
   private async Task<bool> WaitReconnectAsync(ReconnectInfo reconnectInfo, CancellationToken ct)
   {
       int retryCount = 0;
       const int maxRetries = 3;
       const int scanDuration = 10000; // 10秒
       
       while (retryCount < maxRetries && !ct.IsCancellationRequested)
       {
           XTrace.WriteLine($"回连尝试 {retryCount + 1}/{maxRetries}");
           
           // 开始扫描
           _bleService.StartScan();
           await Task.Delay(scanDuration, ct);
           _bleService.StopScan();
           
           // 检查是否找到设备
           var targetDevice = FindReconnectDevice(reconnectInfo);
           if (targetDevice != null)
           {
               if (await _bleService.ConnectAndSubscribeAsync(targetDevice))
               {
                   XTrace.WriteLine("回连成功");
                   return true;
               }
           }
           
           retryCount++;
           await Task.Delay(2000, ct); // 等待2秒后重试
       }
       
       XTrace.WriteLine("回连失败");
       return false;
   }
   ```

### 3.4 升级失败

**症状**: 查询升级结果返回失败

**错误码对照**:

| 错误码 | 说明 | 解决方案 |
|--------|------|---------|
| 0x01 | 数据校验错误 | 重新传输 |
| 0x02 | 升级失败 | 检查固件文件 |
| 0x03 | 密钥不匹配 | 使用正确的加密固件 |
| 0x04 | 升级文件错误 | 更换固件文件 |
| 0x07 | Flash读取错误 | 设备硬件问题 |

**排查步骤**:

1. **查询详细结果**
   ```csharp
   var resultCmd = RcspCommandFactory.CreateCommand<CmdQueryUpdateResult>();
   var resultRsp = await _rcspProtocol.SendCommandAsync<RspUpdateResult>(resultCmd);
   
   XTrace.WriteLine($"升级结果: {resultRsp.Result:X2}");
   
   if (resultRsp.Result != RspUpdateResult.RESULT_COMPLETE)
   {
       var errorMsg = GetUpdateResultErrorMessage(resultRsp.Result);
       XTrace.WriteLine($"升级失败: {errorMsg}");
       
       // 根据错误类型决定是否重试
       if (resultRsp.Result == RspUpdateResult.RESULT_DATA_CHECK_ERROR)
       {
           XTrace.WriteLine("数据校验失败，可重试");
       }
   }
   ```

2. **验证文件完整性**
   ```csharp
   private void ValidateFileBeforeUpgrade()
   {
       // 计算整个文件的CRC
       var crc = _fileService.CalculateCrc16(_fileData);
       XTrace.WriteLine($"文件CRC16: {crc:X4}");
       
       // 检查文件大小
       XTrace.WriteLine($"文件大小: {_fileData.Length} bytes");
       
       // 检查文件头
       var header = _fileData.Take(16).ToArray();
       XTrace.WriteLine($"文件头: {BytesToHex(header)}");
   }
   ```

---

## 四、性能问题

### 4.1 传输速度慢

**症状**: 升级时间过长，传输速度 < 5KB/s

**可能原因**:
- MTU过小
- 发送间隔过长
- 使用Write而非WriteWithoutResponse
- CPU占用高

**优化方案**:

1. **协商最大MTU**
   ```csharp
   var session = await GattSession.FromDeviceIdAsync(deviceId);
   session.MaintainConnection = true;
   
   // 请求最大MTU（512字节）
   await session.RequestMaxPduSizeAsync(512);
   
   var mtu = session.MaxPduSize;
   XTrace.WriteLine($"协商MTU: {mtu}");
   ```

2. **使用WriteWithoutResponse**
   ```csharp
   // 优先使用WriteWithoutResponse特征值
   var writeChar = characteristics.FirstOrDefault(c =>
       c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse));
   
   if (writeChar != null)
   {
       // 无需等待设备确认，速度更快
       await writeChar.WriteValueWithResultAsync(buffer, GattWriteOption.WriteWithoutResponse);
   }
   ```

3. **减少发送间隔**
   ```csharp
   private async Task SendLargeDataAsync(byte[] data)
   {
       int mtu = _currentMtu - 3;
       
       for (int i = 0; i < data.Length; i += mtu)
       {
           // ...
           await WriteCharacteristicAsync(chunk);
           
           // 减少延迟（但要避免过快导致丢包）
           if (i % (mtu * 10) == 0) // 每10包延迟一次
           {
               await Task.Delay(5);
           }
       }
   }
   ```

4. **监控传输速度**
   ```csharp
   private DateTime _lastProgressTime = DateTime.Now;
   private int _lastTransferredBytes = 0;
   
   private void UpdateTransferSpeed()
   {
       var now = DateTime.Now;
       var elapsed = (now - _lastProgressTime).TotalSeconds;
       
       if (elapsed >= 1.0) // 每秒更新一次
       {
           var bytesDiff = _transferredBytes - _lastTransferredBytes;
           var speed = bytesDiff / elapsed;
           
           XTrace.WriteLine($"传输速度: {speed:F2} bytes/s ({speed / 1024:F2} KB/s)");
           
           _lastProgressTime = now;
           _lastTransferredBytes = _transferredBytes;
       }
   }
   ```

### 4.2 内存占用高

**症状**: 升级时内存持续增长

**可能原因**:
- 日志缓冲区过大
- 数据包未及时释放
- 事件处理器未注销

**优化方案**:

1. **限制日志大小**
   ```csharp
   private readonly Queue<string> _logBuffer = new();
   private const int MAX_LOG_COUNT = 1000;
   
   private void AddLog(string message)
   {
       if (_logBuffer.Count >= MAX_LOG_COUNT)
       {
           _logBuffer.Dequeue();
       }
       _logBuffer.Enqueue(message);
   }
   ```

2. **及时释放资源**
   ```csharp
   public void Release()
   {
       // 注销事件
       if (_notifyCharacteristic != null)
       {
           _notifyCharacteristic.ValueChanged -= OnCharacteristicValueChanged;
       }
       
       // 清空缓冲区
       _parser.Clear();
       _pendingCommands.Clear();
       _fileData = null;
   }
   ```

3. **使用对象池**
   ```csharp
   private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
   
   private async Task ProcessDataAsync(int length)
   {
       byte[] buffer = _bufferPool.Rent(length);
       try
       {
           // 使用buffer
       }
       finally
       {
           _bufferPool.Return(buffer);
       }
   }
   ```

---

## 五、调试工具

### 5.1 数据包分析工具

创建一个简单的数据包分析器：

```csharp
public class PacketAnalyzer
{
    public void AnalyzePacket(byte[] data)
    {
        Console.WriteLine("=== 数据包分析 ===");
        Console.WriteLine($"原始数据: {BytesToHex(data)}");
        Console.WriteLine($"长度: {data.Length} bytes");
        
        if (data.Length < 6)
        {
            Console.WriteLine("数据包过短");
            return;
        }
        
        // 解析帧头
        if (data[0] == 0xAA && data[1] == 0x55)
        {
            Console.WriteLine("✓ 帧头正确");
        }
        else
        {
            Console.WriteLine("✗ 帧头错误");
        }
        
        // 解析FLAG
        byte flag = data[2];
        bool isCommand = (flag & 0x80) != 0;
        bool needResponse = (flag & 0x40) != 0;
        Console.WriteLine($"FLAG: {flag:X2} (命令={isCommand}, 需响应={needResponse})");
        
        // 解析SN和OpCode
        byte sn = data[3];
        byte opCode = data[4];
        Console.WriteLine($"SN: {sn}");
        Console.WriteLine($"OpCode: {opCode:X2} ({GetOpCodeName(opCode)})");
        
        // 解析Payload
        if (data.Length > 6)
        {
            int payloadLength = data.Length - 6;
            byte[] payload = new byte[payloadLength];
            Buffer.BlockCopy(data, 5, payload, 0, payloadLength);
            Console.WriteLine($"Payload: {BytesToHex(payload)} ({payloadLength} bytes)");
        }
        
        // 检查帧尾
        if (data[^1] == 0xAD)
        {
            Console.WriteLine("✓ 帧尾正确");
        }
        else
        {
            Console.WriteLine("✗ 帧尾错误");
        }
    }
    
    private string GetOpCodeName(byte opCode)
    {
        return opCode switch
        {
            0x02 => "GetTargetInfo",
            0xE0 => "GetFileOffset",
            0xE1 => "InquireCanUpdate",
            0xE2 => "EnterUpdateMode",
            0xE4 => "SendFileBlock",
            0xE5 => "QueryUpdateResult",
            0xE6 => "RebootDevice",
            0xE7 => "NotifyFileSize",
            _ => "Unknown"
        };
    }
}
```

### 5.2 日志记录

创建详细的日志记录：

```csharp
public class OtaLogger
{
    private readonly string _logFilePath;
    
    public OtaLogger(string deviceId)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _logFilePath = $"ota_log_{deviceId}_{timestamp}.txt";
    }
    
    public void Log(string level, string message)
    {
        var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        
        // 输出到控制台
        XTrace.WriteLine(logEntry);
        
        // 写入文件
        File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
    }
    
    public void LogPacket(string direction, byte[] data)
    {
        Log("PACKET", $"{direction}: {BytesToHex(data)}");
    }
    
    public void LogState(OtaState state)
    {
        Log("STATE", $"状态变更: {state}");
    }
    
    public void LogProgress(int percentage, long speed)
    {
        Log("PROGRESS", $"进度: {percentage}%, 速度: {speed / 1024:F2} KB/s");
    }
}
```

### 5.3 性能监控

```csharp
public class PerformanceMonitor
{
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();
    private readonly Dictionary<string, TimeSpan> _timings = new();
    
    public void StartTimer(string name)
    {
        _stopwatch.Restart();
    }
    
    public void StopTimer(string name)
    {
        _stopwatch.Stop();
        _timings[name] = _stopwatch.Elapsed;
        
        XTrace.WriteLine($"[PERF] {name}: {_stopwatch.ElapsedMilliseconds} ms");
    }
    
    public void PrintSummary()
    {
        XTrace.WriteLine("=== 性能统计 ===");
        foreach (var kvp in _timings)
        {
            XTrace.WriteLine($"{kvp.Key}: {kvp.Value.TotalSeconds:F2}秒");
        }
    }
}

// 使用示例
var perfMon = new PerformanceMonitor();

perfMon.StartTimer("文件校验");
await ValidateFileAsync();
perfMon.StopTimer("文件校验");

perfMon.StartTimer("RCSP初始化");
await _rcspProtocol.InitializeAsync(device);
perfMon.StopTimer("RCSP初始化");

// ...

perfMon.PrintSummary();
```

---

## 六、常见错误代码

| 错误码 | 名称 | 说明 | 解决方案 |
|--------|------|------|---------|
| -97 | ERROR_OTA_LOW_POWER | 设备电量过低 | 为设备充电 |
| -99 | ERROR_OTA_FIRMWARE_VERSION_NO_CHANGE | 固件版本未变化 | 使用更高版本的固件 |
| -100 | ERROR_OTA_TWS_NOT_CONNECT | TWS未连接 | 确保双耳连接 |
| -102 | ERROR_OTA_DATA_CHECK_ERROR | 数据校验错误 | 重新传输或更换固件 |
| -103 | ERROR_OTA_FAIL | 升级失败 | 查看详细日志 |
| -104 | ERROR_OTA_ENCRYPTED_KEY_NOT_MATCH | 加密密钥不匹配 | 使用正确的固件 |
| -111 | ERROR_OTA_COMMAND_TIMEOUT | 命令超时 | 检查连接状态，增加超时时间 |
| -112 | ERROR_OTA_RECONNECT_DEVICE_TIMEOUT | 回连设备超时 | 检查回连逻辑，增加超时时间 |

---

## 七、技术支持

如果上述方法无法解决问题，请提供以下信息寻求技术支持：

1. **日志文件**：包含完整的升级过程日志
2. **数据包记录**：发送和接收的原始数据包
3. **设备信息**：设备型号、固件版本、电量等
4. **升级文件**：使用的固件文件名称和大小
5. **错误信息**：具体的错误码和错误描述
6. **复现步骤**：详细的操作步骤

**联系方式**:
- 杰理官方文档: https://doc.zh-jieli.com/vue/#/docs/ota
- GitHub Issues: https://github.com/PeiKeSmart/Ava.Ble

---

**文档版本**: v1.0  
**创建日期**: 2025-11-04  
**维护人**: PeiKeSmart Team
