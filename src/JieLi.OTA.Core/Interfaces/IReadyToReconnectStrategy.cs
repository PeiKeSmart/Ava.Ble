namespace JieLi.OTA.Core.Interfaces;

/// <summary>
/// 准备重连策略扩展点（对应小程序 SDK 的 it() 设备族/模式特定动作）。
/// 默认实现可以为空（No-Op），具体机型可提供定制策略以做到完全一致。
/// </summary>
public interface IReadyToReconnectStrategy
{
    /// <summary>
    /// 执行“准备重连”阶段动作。
    /// </summary>
    /// <param name="device">当前设备（可能为 Mock 或真实设备）。</param>
    /// <param name="config">OTA 配置。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ExecuteAsync(IBluetoothDevice device, Models.OtaConfig config, CancellationToken cancellationToken = default);
}
