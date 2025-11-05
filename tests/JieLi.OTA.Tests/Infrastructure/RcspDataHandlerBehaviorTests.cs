using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JieLi.OTA.Core.Interfaces;
using JieLi.OTA.Core.Protocols;
using JieLi.OTA.Infrastructure.Bluetooth;
using Xunit;

namespace JieLi.OTA.Tests.Infrastructure;

public class RcspDataHandlerBehaviorTests
{
    [Fact(DisplayName = "0xE8 通知应立即 ACK [Status,Sn]")]
    public async Task NotifyUpgradeSize_ShouldAckImmediately()
    {
        var device = new MockBleDevice();
        var handler = new RcspDataHandler(device);
        await handler.InitializeAsync();

        // 构造 0xE8 命令: [Sn, total(4), current(4)]
        byte sn = 0x07;
        uint total = 1024;
        uint current = 256;
        var payload = new List<byte> { sn };
        payload.AddRange(BitConverter.GetBytes(total));
        payload.AddRange(BitConverter.GetBytes(current));

    var packet = new RcspPacket { Flag = RcspPacket.FLAG_IS_COMMAND, OpCode = OtaOpCode.CMD_OTA_NOTIFY_FILE_SIZE, Payload = payload.ToArray() };
        device.SimulateDataReceived(packet.ToBytes());

        // 设备端应收到一个响应包，OpCode=0xE8, Payload=[Status(0x00), Sn]
        Assert.True(device.Writes.Count >= 1);
        var bytes = device.Writes[^1];
        var parser = new RcspParser();
        parser.AddData(bytes);
        var resp = parser.TryParse();
        Assert.NotNull(resp);
        Assert.False(resp!.IsCommand);
        Assert.Equal(OtaOpCode.CMD_OTA_NOTIFY_FILE_SIZE, resp.OpCode);
        Assert.True(resp.Payload.Length >= 2);
        Assert.Equal(0x00, resp.Payload[0]);
        Assert.Equal(sn, resp.Payload[1]);
    }

    [Fact(DisplayName = "0xE5 去重：相同 Sn 在 50ms 内仅处理一次")]
    public async Task FileBlockRequest_DuplicateSn_ShouldBeIgnoredWithin50ms()
    {
        var device = new MockBleDevice();
        var handler = new RcspDataHandler(device);
        await handler.InitializeAsync();

        int received = 0;
        handler.OnDeviceCommandReceived += (_, __) => Interlocked.Increment(ref received);

        byte sn = 0x11;
        int offset1 = 100;
        ushort len1 = 32;
        var p1 = new List<byte> { sn };
        p1.AddRange(BitConverter.GetBytes(offset1));
        p1.AddRange(BitConverter.GetBytes(len1));
    var pkt1 = new RcspPacket { Flag = RcspPacket.FLAG_IS_COMMAND, OpCode = OtaOpCode.CMD_OTA_FILE_BLOCK, Payload = p1.ToArray() };

        int offset2 = 200; // 不同 offset 但相同 sn
        ushort len2 = 32;
        var p2 = new List<byte> { sn };
        p2.AddRange(BitConverter.GetBytes(offset2));
        p2.AddRange(BitConverter.GetBytes(len2));
    var pkt2 = new RcspPacket { Flag = RcspPacket.FLAG_IS_COMMAND, OpCode = OtaOpCode.CMD_OTA_FILE_BLOCK, Payload = p2.ToArray() };

        device.SimulateDataReceived(pkt1.ToBytes());
        // 小于 50ms 再次发送
        await Task.Delay(10);
        device.SimulateDataReceived(pkt2.ToBytes());

        Assert.Equal(1, received);
    }

    [Fact(DisplayName = "0xE5 去重：相同 (offset,len) 在 50ms 内仅处理一次")]
    public async Task FileBlockRequest_DuplicateBlock_ShouldBeIgnoredWithin50ms()
    {
        var device = new MockBleDevice();
        var handler = new RcspDataHandler(device);
        await handler.InitializeAsync();

        int received = 0;
        handler.OnDeviceCommandReceived += (_, __) => Interlocked.Increment(ref received);

        int offset = 4096;
        ushort len = 128;

        byte sn1 = 0x21;
        var p1 = new List<byte> { sn1 };
        p1.AddRange(BitConverter.GetBytes(offset));
        p1.AddRange(BitConverter.GetBytes(len));
    var pkt1 = new RcspPacket { Flag = RcspPacket.FLAG_IS_COMMAND, OpCode = OtaOpCode.CMD_OTA_FILE_BLOCK, Payload = p1.ToArray() };

        byte sn2 = 0x22; // 不同 sn 但相同 (offset,len)
        var p2 = new List<byte> { sn2 };
        p2.AddRange(BitConverter.GetBytes(offset));
        p2.AddRange(BitConverter.GetBytes(len));
    var pkt2 = new RcspPacket { Flag = RcspPacket.FLAG_IS_COMMAND, OpCode = OtaOpCode.CMD_OTA_FILE_BLOCK, Payload = p2.ToArray() };

        device.SimulateDataReceived(pkt1.ToBytes());
        await Task.Delay(10);
        device.SimulateDataReceived(pkt2.ToBytes());

        Assert.Equal(1, received);
    }

    private class MockBleDevice : IBluetoothDevice
    {
        public string DeviceId => "mock";
        public string DeviceName => "mock";
        public short Rssi => -42;
        public ulong BluetoothAddress => 0xABCDEF0123456789;
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
        public void SimulateDataReceived(byte[] data) => DataReceived?.Invoke(this, data);
        public void UpdateInfo(int rssi, string? deviceName = null) { }
        public void Dispose() { }
    }
}
