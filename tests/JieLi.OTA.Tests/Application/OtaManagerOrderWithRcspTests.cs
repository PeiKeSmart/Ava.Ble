using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JieLi.OTA.Application.Services;
using JieLi.OTA.Core.Interfaces;
using JieLi.OTA.Core.Models;
using JieLi.OTA.Core.Protocols;
using JieLi.OTA.Core.Protocols.Responses;
using JieLi.OTA.Infrastructure.FileSystem;
using Xunit;

namespace JieLi.OTA.Tests.Application;

public class OtaManagerOrderWithRcspTests
{
    [Fact(DisplayName = "集成：真实 RcspProtocol 下 ACK(0xE5) 必先于 0xE6 命令")]
    public async Task AckThenE6_WithRealRcspProtocol()
    {
        // Arrange
        var device = new RealMockDevice();
        var protocol = new RcspProtocol(device);
        await protocol.InitializeAsyncForTest();

        var manager = new TestableOtaManager(new MockWindowsBleService(), new OtaFileService());
        manager.TestInject(device, protocol, new byte[2048], OtaState.TransferringFile);

        // 构造设备 0xE5 命令 (offset=0,len=0)
        byte sn = 0x55;
        var payload = new List<byte> { sn };
        payload.AddRange(BitConverter.GetBytes(0)); // offset=0
        payload.AddRange(BitConverter.GetBytes((ushort)0)); // len=0
        var e5Command = new RcspPacket { Flag = RcspPacket.FLAG_IS_COMMAND, OpCode = OtaOpCode.CMD_OTA_FILE_BLOCK, Payload = payload.ToArray() };

        // Act
        manager.InvokeDeviceRequestedFileBlock(e5Command);

        // 等待异步链路写入与 0xE6 响应往返
        await Task.Delay(80);

        // Assert - 检查设备写入顺序：第一包 0xE5 响应，第二包 0xE6 命令
        Assert.True(device.Writes.Count >= 2, $"Writes={device.Writes.Count}");
        var first = RcspPacket.Parse(device.Writes[0]);
        var second = RcspPacket.Parse(device.Writes[1]);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.False(first!.IsCommand);
        Assert.Equal(OtaOpCode.CMD_OTA_FILE_BLOCK, first.OpCode);
        Assert.True(second!.IsCommand);
        Assert.Equal(OtaOpCode.CMD_OTA_QUERY_UPDATE_RESULT, second.OpCode);
    }

    private class TestableOtaManager : OtaManager
    {
        public TestableOtaManager(JieLi.OTA.Infrastructure.Bluetooth.WindowsBleService bleService, OtaFileService fileService) : base(bleService, fileService) { }
        public void InvokeDeviceRequestedFileBlock(RcspPacket packet) => OnDeviceRequestedFileBlock(this, packet);
        public new void TestInject(IBluetoothDevice device, IRcspProtocol protocol, byte[] fw, OtaState state) => base.TestInject(device, protocol, fw, state);
    }

    private class MockWindowsBleService : JieLi.OTA.Infrastructure.Bluetooth.WindowsBleService { }

    private class RealMockDevice : IBluetoothDevice
    {
        public string DeviceId => "mock";
        public string DeviceName => "mock";
        public short Rssi => -42;
        public bool IsConnected => true;
        public event EventHandler<byte[]>? DataReceived;
        public event EventHandler<bool>? ConnectionStatusChanged;

        public List<byte[]> Writes { get; } = new();
        private Action<byte[]>? _onDataReceived;

        public Task<bool> ConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<bool> WriteAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            Writes.Add(data);
            // 如果是 0xE6 命令，立即伪造一个响应 [Status, Sn, ResultCode]
            var packet = RcspPacket.Parse(data);
            if (packet != null && packet.IsCommand && packet.OpCode == OtaOpCode.CMD_OTA_QUERY_UPDATE_RESULT && packet.Payload.Length >= 1)
            {
                byte sn = packet.Payload[0];
                var resp = new RcspPacket
                {
                    Flag = 0x00,
                    OpCode = OtaOpCode.CMD_OTA_QUERY_UPDATE_RESULT,
                    Payload = new byte[] { 0x00, sn, 0x00 } // Status=0, Sn, ResultCode=0
                };
                _onDataReceived?.Invoke(resp.ToBytes());
            }
            return Task.FromResult(true);
        }
        public Task<bool> SubscribeNotifyAsync(Action<byte[]> onDataReceived, CancellationToken cancellationToken = default)
        {
            _onDataReceived = onDataReceived;
            return Task.FromResult(true);
        }
        public void UpdateInfo(int rssi, string? deviceName = null) { }
    }
}
