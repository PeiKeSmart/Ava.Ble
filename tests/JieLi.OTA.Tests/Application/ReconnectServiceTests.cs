using JieLi.OTA.Application.Services;
using JieLi.OTA.Infrastructure.Bluetooth;
using Xunit;

namespace JieLi.OTA.Tests.Application;

/// <summary>ReconnectService 单元测试</summary>
public class ReconnectServiceTests
{
    private readonly ReconnectService _service;
    private readonly WindowsBleService _bleService;

    public ReconnectServiceTests()
    {
        _bleService = new WindowsBleService();
        _service = new ReconnectService(_bleService);
    }

    [Fact(DisplayName = "等待重连超时测试")]
    public async Task WaitForReconnectAsync_ShouldTimeoutWhenNoDevice()
    {
        // Arrange
        var originalMac = 0x112233445566UL;
        var timeoutMs = 100; // 100ms 快速超时

        // Act
        var result = await _service.WaitForReconnectAsync(originalMac, useNewMacMethod: true, timeoutMs, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "等待重连取消测试")]
    public async Task WaitForReconnectAsync_ShouldCancelWhenTokenCancelled()
    {
        // Arrange
        var originalMac = 0x112233445566UL;
        var cts = new CancellationTokenSource();
        cts.CancelAfter(50); // 50ms 后取消

        // Act
        var result = await _service.WaitForReconnectAsync(originalMac, useNewMacMethod: true, 5000, cts.Token);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "使用新MAC方法等待重连")]
    public async Task WaitForReconnectAsync_ShouldUseNewMacMethod()
    {
        // Arrange
        var originalMac = 0x112233445566UL;
        var timeoutMs = 100;

        // Act - 新方法：低3字节+1
        var result = await _service.WaitForReconnectAsync(originalMac, useNewMacMethod: true, timeoutMs, CancellationToken.None);

        // Assert - 超时但不会抛异常
        Assert.Null(result);
    }

    [Fact(DisplayName = "使用旧MAC方法等待重连")]
    public async Task WaitForReconnectAsync_ShouldUseOldMacMethod()
    {
        // Arrange
        var originalMac = 0x112233445566UL;
        var timeoutMs = 100;

        // Act - 旧方法：最低字节+2
        var result = await _service.WaitForReconnectAsync(originalMac, useNewMacMethod: false, timeoutMs, CancellationToken.None);

        // Assert - 超时但不会抛异常
        Assert.Null(result);
    }

    [Fact(DisplayName = "并发取消多个重连等待")]
    public async Task WaitForReconnectAsync_ShouldHandleConcurrentCancellations()
    {
        // Arrange
        var originalMac = 0x112233445566UL;
        var cts1 = new CancellationTokenSource();
        var cts2 = new CancellationTokenSource();

        cts1.CancelAfter(50);
        cts2.CancelAfter(100);

        // Act - 并发执行多个等待
        var task1 = _service.WaitForReconnectAsync(originalMac, true, 5000, cts1.Token);
        var task2 = _service.WaitForReconnectAsync(originalMac, false, 5000, cts2.Token);

        var results = await Task.WhenAll(task1, task2);

        // Assert
        Assert.All(results, r => Assert.Null(r));
    }
}
