using JieLi.OTA.Core.Interfaces;

namespace JieLi.OTA.Application.Services;

/// <summary>
/// 默认的准备重连策略：不做任何设备特定动作（No-Op）。
/// </summary>
public class NoopReadyToReconnectStrategy : IReadyToReconnectStrategy
{
    public Task ExecuteAsync(IBluetoothDevice device, JieLi.OTA.Core.Models.OtaConfig config, CancellationToken cancellationToken = default)
    {
        // 预留扩展点：默认不做任何动作
        return Task.CompletedTask;
    }
}
