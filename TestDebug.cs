using JieLi.OTA.Core.Protocols;

// 测试协议序列化/反序列化
var packet1 = new RcspPacket
{
    Flag = 0xC0, // 命令包
    OpCode = 0x02,
    Payload = []
};

var bytes1 = packet1.ToBytes();
Console.WriteLine($"命令包: {BitConverter.ToString(bytes1)}");

var packet2 = new RcspPacket
{
    Flag = 0x01, // 响应包
    OpCode = 0x02,
    Payload = [0x01, 0x02, 0x03]
};

var bytes2 = packet2.ToBytes();
Console.WriteLine($"响应包: {BitConverter.ToString(bytes2)}");

var parsed = RcspPacket.Parse(bytes2);
if (parsed != null)
{
    Console.WriteLine($"解析成功: OpCode=0x{parsed.OpCode:X2}, IsCommand={parsed.IsCommand}, PayloadLen={parsed.Payload.Length}");
}
else
{
    Console.WriteLine("解析失败");
}
