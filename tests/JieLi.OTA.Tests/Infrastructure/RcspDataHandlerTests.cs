using JieLi.OTA.Core.Interfaces;
using JieLi.OTA.Core.Protocols;
using JieLi.OTA.Core.Protocols.Commands;
using JieLi.OTA.Core.Protocols.Responses;
using JieLi.OTA.Infrastructure.Bluetooth;

namespace JieLi.OTA.Tests.Infrastructure;

public class RcspDataHandlerTests
{
    #region SendCommandAsync Tests

    [Fact(DisplayName = "发送命令超时应抛出异常")]
    public async Task SendCommandAsync_Timeout_ShouldThrowTimeoutException()
    {
        // Arrange
        var mockDevice = new MockBleDevice();
        var handler = new RcspDataHandler(mockDevice);
        await handler.InitializeAsync();

        var command = new CmdGetTargetInfo();

        // 不模拟响应，让命令超时

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await handler.SendCommandAsync<RspDeviceInfo>(command, 100);
        });
    }

    #endregion

    #region Mock Classes

    class MockBleDevice : IBluetoothDevice
    {
        public string DeviceId => "mock_device";
        public string DeviceName => "Mock Device";
        public short Rssi => -50;
        public ulong BluetoothAddress => 0x123456789ABC;
        public bool IsConnected { get; private set; }

        public event EventHandler<byte[]>? DataReceived;
        public event EventHandler<bool>? ConnectionStatusChanged;

        public Action<byte[]>? OnDataWritten { get; set; }

        public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            ConnectionStatusChanged?.Invoke(this, true);
            return Task.FromResult(true);
        }

        public Task DisconnectAsync()
        {
            IsConnected = false;
            ConnectionStatusChanged?.Invoke(this, false);
            return Task.CompletedTask;
        }

        public Task<bool> WriteAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            OnDataWritten?.Invoke(data);
            return Task.FromResult(true);
        }

        public Task<bool> SubscribeNotifyAsync(Action<byte[]> onDataReceived, CancellationToken cancellationToken = default)
        {
            DataReceived += (sender, data) => onDataReceived(data);
            return Task.FromResult(true);
        }

        public void SimulateDataReceived(byte[] data)
        {
            DataReceived?.Invoke(this, data);
        }

        public void UpdateInfo(int rssi, string? deviceName = null) { }

        public void Dispose()
        {
            IsConnected = false;
        }
    }

    #endregion
}
