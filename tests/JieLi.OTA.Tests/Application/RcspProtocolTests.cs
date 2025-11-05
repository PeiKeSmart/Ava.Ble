using JieLi.OTA.Application.Services;
using JieLi.OTA.Core.Interfaces;
using JieLi.OTA.Core.Protocols;
using JieLi.OTA.Core.Protocols.Commands;
using JieLi.OTA.Core.Protocols.Responses;
using JieLi.OTA.Infrastructure.Bluetooth;
using NewLife.Data;
using Xunit;

namespace JieLi.OTA.Tests.Application;

/// <summary>RcspProtocol 单元测试</summary>
public class RcspProtocolTests : IDisposable
{
    private readonly RcspProtocol _protocol;
    private readonly MockBluetoothDevice _mockDevice;

    public RcspProtocolTests()
    {
        _mockDevice = new MockBluetoothDevice();
        _protocol = new RcspProtocol(_mockDevice);
    }

    [Fact(DisplayName = "初始化协议 - 成功获取设备信息")]
    public async Task InitializeAsync_ShouldReturnDeviceInfo()
    {
        // Arrange
        var deviceId = "test-device";
        var payload = BuildDeviceInfoPayload("JL_Device", "1.0.0", 100, 1, 80, true, "AA:BB:CC:DD:EE:FF", 0);
        _mockDevice.SetupResponse((byte)0x02, payload);

        // Act
        var deviceInfo = await _protocol.InitializeAsync(deviceId, CancellationToken.None);

        // Assert
        Assert.NotNull(deviceInfo);
        Assert.Equal("JL_Device", deviceInfo.DeviceName);
        Assert.Equal("1.0.0", deviceInfo.VersionName);
        Assert.Equal(100u, deviceInfo.VersionCode);
        Assert.Equal(80, deviceInfo.BatteryLevel);
    }

    [Fact(DisplayName = "查询是否可更新 - 设备支持更新")]
    public async Task InquireCanUpdateAsync_ShouldReturnTrue_WhenDeviceSupportsUpdate()
    {
        // Arrange
        await InitializeProtocol();
        _mockDevice.SetupResponse(0xE2, BuildCanUpdatePayload(RspCanUpdate.RESULT_CAN_UPDATE)); // CMD_OTA_INQUIRE_CAN_UPDATE

        // Act
        var canUpdate = await _protocol.InquireCanUpdateAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(canUpdate);
        Assert.True(canUpdate.CanUpdate);
        Assert.Equal(RspCanUpdate.RESULT_CAN_UPDATE, canUpdate.Result);
    }

    [Fact(DisplayName = "查询是否可更新 - 设备电量不足")]
    public async Task InquireCanUpdateAsync_ShouldReturnFalse_WhenLowPower()
    {
        // Arrange
        await InitializeProtocol();
        _mockDevice.SetupResponse(0xE2, BuildCanUpdatePayload(RspCanUpdate.RESULT_LOW_POWER)); // CMD_OTA_INQUIRE_CAN_UPDATE

        // Act
        var canUpdate = await _protocol.InquireCanUpdateAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(canUpdate);
        Assert.False(canUpdate.CanUpdate);
        Assert.Equal(RspCanUpdate.RESULT_LOW_POWER, canUpdate.Result);
    }

    [Fact(DisplayName = "读取文件偏移 - 返回正确偏移量")]
    public async Task ReadFileOffsetAsync_ShouldReturnOffset()
    {
        // Arrange
        await InitializeProtocol();
        _mockDevice.SetupResponse(0xE1, BuildFileOffsetPayload(1024)); // CMD_OTA_READ_FILE_OFFSET

        // Act
        var fileOffset = await _protocol.ReadFileOffsetAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(fileOffset);
        Assert.Equal(1024u, fileOffset.Offset);
    }

    [Fact(DisplayName = "进入更新模式 - 成功")]
    public async Task EnterUpdateModeAsync_ShouldReturnTrue_WhenSuccess()
    {
        // Arrange
        await InitializeProtocol();
        _mockDevice.SetupResponse(0xE3, BuildCanUpdatePayload(RspCanUpdate.RESULT_CAN_UPDATE)); // CMD_OTA_ENTER_UPDATE_MODE

        // Act
        var success = await _protocol.EnterUpdateModeAsync(CancellationToken.None);

        // Assert
        Assert.True(success);
    }

    [Fact(DisplayName = "通知文件大小 - 成功")]
    public async Task NotifyFileSizeAsync_ShouldReturnTrue_WhenSuccess()
    {
        // Arrange
        await InitializeProtocol();
        _mockDevice.SetupResponse(0xE8, BuildFileOffsetPayload(0)); // CMD_OTA_NOTIFY_FILE_SIZE

        // Act
        var success = await _protocol.NotifyFileSizeAsync(1024, CancellationToken.None);

        // Assert
        Assert.True(success);
    }

    [Fact(DisplayName = "断开连接 - 正常清理")]
    public async Task DisconnectAsync_ShouldCleanupResources()
    {
        // Arrange
        await InitializeProtocol();

        // Act
        await _protocol.DisconnectAsync();

        // Assert - 无异常即为成功
        Assert.True(true);
    }

    private async Task InitializeProtocol()
    {
        var payload = BuildDeviceInfoPayload("TestDevice", "1.0.0", 100, 1, 80, false, "00:00:00:00:00:00", 0);
        _mockDevice.SetupResponse(0x02, payload);

        await _protocol.InitializeAsync("test-device", CancellationToken.None);
    }

    public void Dispose()
    {
        _protocol.DisconnectAsync().Wait();
    }

    /// <summary>模拟蓝牙设备（用于测试）</summary>
    private class MockBluetoothDevice : IBluetoothDevice
    {
        private readonly Dictionary<byte, byte[]> _responses = new();
        private Action<byte[]>? _dataCallback;
        private bool _isSubscribed;

        public string DeviceId => "mock-device-id";
        public string DeviceName => "MockDevice";
        public short Rssi => -50;

        public event EventHandler<byte[]>? DataReceived;
        public event EventHandler<bool>? ConnectionStatusChanged;

        public void SetupResponse(byte opCode, byte[] payload)
        {
            // 响应 Payload 格式: [Status, Sn, ...业务数据]
            // Status=0 表示成功
            var responsePayload = new byte[2 + payload.Length];
            responsePayload[0] = 0x00; // Status = SUCCESS
            responsePayload[1] = 0x00; // Sn = 0 (将在 WriteAsync 中动态替换)
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, responsePayload, 2, payload.Length);
            }
            
            // 构建响应包
            var packet = new RcspPacket
            {
                Flag = 0x01, // 响应标志 (非命令、非需响应)
                OpCode = opCode,
                Payload = responsePayload
            };
            _responses[opCode] = packet.ToBytes();
        }

        public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            ConnectionStatusChanged?.Invoke(this, true);
            return Task.FromResult(true);
        }

        public Task DisconnectAsync()
        {
            ConnectionStatusChanged?.Invoke(this, false);
            return Task.CompletedTask;
        }

        public Task<bool> SubscribeNotifyAsync(Action<byte[]> onDataReceived, CancellationToken cancellationToken = default)
        {
            _dataCallback = onDataReceived;
            _isSubscribed = true;
            return Task.FromResult(true);
        }

        public Task<bool> WriteAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            if (!_isSubscribed || _dataCallback == null)
            {
                System.Diagnostics.Debug.WriteLine($"[Mock] WriteAsync 拒绝: IsSubscribed={_isSubscribed}, HasCallback={_dataCallback != null}");
                return Task.FromResult(false);
            }

            // 模拟设备响应 - 在新线程异步执行
            _ = Task.Run(async () =>
            {
                await Task.Delay(10); // 模拟网络延迟
                
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[Mock] 收到数据: {BitConverter.ToString(data)}");
                    var packet = RcspPacket.Parse(data);
                    if (packet == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Mock] 解析失败");
                        return;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[Mock] 解析成功: OpCode=0x{packet.OpCode:X2}, IsCommand={packet.IsCommand}, PayloadLen={packet.Payload.Length}");
                    
                    // 从命令 Payload 中提取 Sn (Command Payload: [Sn, ...])
                    byte commandSn = packet.Payload.Length > 0 ? packet.Payload[0] : (byte)0;
                    System.Diagnostics.Debug.WriteLine($"[Mock] 提取命令 Sn={commandSn}");
                    
                    if (_responses.TryGetValue(packet.OpCode, out var responseData))
                    {
                        // 克隆响应数据
                        var response = (byte[])responseData.Clone();
                        
                        // 更新响应 Payload 中的 Sn (Response Payload: [Status, Sn, ...])
                        // Payload 在 response[7..^1] 位置
                        if (response.Length > 8) // 至少有 Payload[0] 和 Payload[1]
                        {
                            response[8] = commandSn; // Payload[1] = Sn
                            System.Diagnostics.Debug.WriteLine($"[Mock] 更新响应 Sn={commandSn}");
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[Mock] 发送响应: {BitConverter.ToString(response)}");
                        _dataCallback?.Invoke(response);
                        DataReceived?.Invoke(this, response);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Mock] 未找到OpCode=0x{packet.OpCode:X2}的响应, 已配置: {string.Join(",", _responses.Keys.Select(k => $"0x{k:X2}"))}");
                    }
                }
                catch (Exception ex)
                {
                    // 测试中的异常
                    System.Diagnostics.Debug.WriteLine($"[Mock] 错误: {ex.Message}\n{ex.StackTrace}");
                }
            });

            return Task.FromResult(true);
        }

        public void Dispose()
        {
            _dataCallback = null;
            _isSubscribed = false;
        }
    }

    #region Payload 构建辅助方法

    /// <summary>构建设备信息响应 Payload</summary>
    private static byte[] BuildDeviceInfoPayload(
        string deviceName, string versionName, uint versionCode,
        byte deviceType, byte batteryLevel, bool supportDoubleBackup,
        string bleMac, byte communicationWay)
    {
        using var ms = new MemoryStream();
        
        // ⚠️ TLV格式 (Type-Length-Value)
        
        // Type 1: 设备名称
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(deviceName);
        ms.WriteByte(1); // Type
        ms.WriteByte((byte)nameBytes.Length); // Length
        ms.Write(nameBytes, 0, nameBytes.Length); // Value
        
        // Type 2: 固件版本
        var versionBytes = System.Text.Encoding.UTF8.GetBytes(versionName);
        ms.WriteByte(2); // Type
        ms.WriteByte((byte)versionBytes.Length); // Length
        ms.Write(versionBytes, 0, versionBytes.Length); // Value
        
        // Type 5: 版本号 (2字节，大端序)
        ms.WriteByte(5); // Type
        ms.WriteByte(2); // Length
        ms.WriteByte((byte)((versionCode >> 8) & 0xFF));
        ms.WriteByte((byte)(versionCode & 0xFF));
        
        // Type 6: SDK类型 / 设备类型
        ms.WriteByte(6); // Type
        ms.WriteByte(1); // Length
        ms.WriteByte(deviceType); // Value
        
        // Type 8: 双备份和BootLoader信息
        ms.WriteByte(8); // Type
        ms.WriteByte(3); // Length
        ms.WriteByte(supportDoubleBackup ? (byte)0x01 : (byte)0x00); // isSupportDoubleBackup
        ms.WriteByte(0x00); // isNeedBootLoader (默认false)
        ms.WriteByte(0x00); // singleBackupOtaWay
        
        // Type 9: 强制升级标志
        ms.WriteByte(9); // Type
        ms.WriteByte(3); // Length
        ms.WriteByte(0x00); // mandatoryUpgradeFlag (默认0=非强制)
        ms.WriteByte(0x00); // requestOtaFlag
        ms.WriteByte(0x00); // expandMode
        
        // Type 21: 电池电量
        ms.WriteByte(21); // Type
        ms.WriteByte(1); // Length
        ms.WriteByte(batteryLevel); // Value
        
        // Type 22: MAC 地址（6字节）
        ms.WriteByte(22); // Type
        ms.WriteByte(6); // Length
        var macParts = bleMac.Split(':');
        foreach (var part in macParts)
        {
            ms.WriteByte(Convert.ToByte(part, 16));
        }
        
        return ms.ToArray();
    }

    /// <summary>构建查询可更新响应 Payload</summary>
    private static byte[] BuildCanUpdatePayload(byte result)
    {
        return [result];
    }

    /// <summary>构建文件偏移响应 Payload</summary>
    private static byte[] BuildFileOffsetPayload(uint offset)
    {
        return BitConverter.GetBytes(offset);
    }

    #endregion
}
