using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JieLi.OTA.Application.Services;
using JieLi.OTA.Core.Interfaces;
using JieLi.OTA.Core.Models;
using JieLi.OTA.Core.Protocols;
using JieLi.OTA.Infrastructure.FileSystem;
using JieLi.OTA.Infrastructure.Bluetooth;
using JieLi.OTA.Core.Protocols.Responses;
using Xunit;

namespace JieLi.OTA.Tests.Application;

public class OtaManagerOrderTests
{
    [Fact(DisplayName = "ACK(0xE5 响应) 必须先于 QueryUpdateResult(0xE6 命令) 发送")]
    public async Task ZeroAck_ShouldBeBefore_QueryUpdateResult_Command()
    {
        // Arrange
        var device = new OrderMockDevice();
        var protocol = new TestRcspProtocol(device);
        var manager = new TestableOtaManager(new MockWindowsBleService(), new OtaFileService());
    manager.Inject(device, protocol, new byte[1024], OtaState.TransferringFile);

        // 构造设备 0xE5 命令 (offset=0,len=0)
        byte sn = 0x33;
        var payload = new List<byte> { sn };
        payload.AddRange(BitConverter.GetBytes(0)); // offset
        payload.AddRange(BitConverter.GetBytes((ushort)0)); // len
        var deviceCmd = new RcspPacket { Flag = RcspPacket.FLAG_IS_COMMAND, OpCode = OtaOpCode.CMD_OTA_FILE_BLOCK, Payload = payload.ToArray() };

        // Act
        manager.InvokeDeviceRequestedFileBlock(deviceCmd);

        // 等待短暂时间让异步写入发生
        await Task.Delay(50);

        // Assert - 至少两次写入：E5 响应在前，E6 命令在后
        Assert.True(device.Writes.Count >= 2);
        var first = RcspPacket.Parse(device.Writes[0]);
        var second = RcspPacket.Parse(device.Writes[1]);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.False(first!.IsCommand); // 响应包
        Assert.Equal(OtaOpCode.CMD_OTA_FILE_BLOCK, first.OpCode); // 0xE5 响应
        Assert.True(second!.IsCommand);
        Assert.Equal(OtaOpCode.CMD_OTA_QUERY_UPDATE_RESULT, second.OpCode); // 0xE6 命令
    }

    private class TestableOtaManager : OtaManager
    {
        public TestableOtaManager(WindowsBleService bleService, OtaFileService fileService) : base(bleService, fileService) { }
        public void InvokeDeviceRequestedFileBlock(RcspPacket packet) => OnDeviceRequestedFileBlock(this, packet);
        public void Inject(IBluetoothDevice device, IRcspProtocol protocol, byte[] fw, OtaState state) => TestInject(device, protocol, fw, state);
    }

    private class MockWindowsBleService : JieLi.OTA.Infrastructure.Bluetooth.WindowsBleService { }

    private class OrderMockDevice : IBluetoothDevice
    {
        public string DeviceId => "mock";
        public string DeviceName => "mock";
        public short Rssi => -42;
        public ulong BluetoothAddress => 0x0011223344556677;
        public bool IsConnected => true;

        public event EventHandler<byte[]>? DataReceived;
        public event EventHandler<bool>? ConnectionStatusChanged;

        public List<byte[]> Writes { get; } = new();

        public Task<bool> ConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<bool> WriteAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            Writes.Add(data);
            return Task.FromResult(true);
        }
        public Task<bool> SubscribeNotifyAsync(Action<byte[]> onDataReceived, CancellationToken cancellationToken = default)
        {
            DataReceived += (_, bytes) => onDataReceived(bytes);
            return Task.FromResult(true);
        }
        public void UpdateInfo(int rssi, string? deviceName = null) { }
        public void Dispose() { }
    }

    private class TestRcspProtocol : IRcspProtocol
    {
        private readonly IBluetoothDevice _device;
        public TestRcspProtocol(IBluetoothDevice device) { _device = device; }

        public Task<RspDeviceInfo> InitializeAsync(string deviceId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<RspCanUpdate> InquireCanUpdateAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<RspFileOffset> ReadFileOffsetAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<bool> EnterUpdateModeAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<bool> NotifyFileSizeAsync(uint fileSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<TResponse> SendCommandAsync<TResponse>(RcspCommand command, int timeoutMs = 5000, CancellationToken cancellationToken = default) where TResponse : RcspResponse, new()
            => throw new NotImplementedException();
        public event EventHandler<RcspPacket>? DeviceRequestedFileBlock { add { } remove { } }
        public Task DisconnectAsync() => Task.CompletedTask;

        public Task<int> ChangeCommunicationWayAsync(byte communicationWay, bool isSupportNewRebootWay, CancellationToken cancellationToken = default)
            => Task.FromResult(0); // Mock 实现，返回成功

        public Task RebootDeviceAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask; // Mock 实现，模拟发送重启命令

        public async Task<RspUpdateResult> QueryUpdateResultAsync(CancellationToken cancellationToken = default)
        {
            // 模拟发送 0xE6 命令（带 NEED_RESPONSE 标志）
            byte sn = 0x01;
            var packet = new RcspPacket
            {
                Flag = (byte)(RcspPacket.FLAG_IS_COMMAND | RcspPacket.FLAG_NEED_RESPONSE),
                OpCode = OtaOpCode.CMD_OTA_QUERY_UPDATE_RESULT,
                Payload = new byte[] { sn }
            };
            await _device.WriteAsync(packet.ToBytes(), cancellationToken);

            // 直接返回一个伪造的响应对象（OtaManager 仅记录日志，不依赖值）
            return new RspUpdateResult { Status = 0x00, ResultCode = 0x00, OpCode = OtaOpCode.CMD_OTA_QUERY_UPDATE_RESULT, Sn = sn };
        }
    }
}
