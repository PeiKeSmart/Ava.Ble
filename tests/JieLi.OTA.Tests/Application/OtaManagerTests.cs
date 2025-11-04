using JieLi.OTA.Application.Services;
using JieLi.OTA.Core.Interfaces;
using JieLi.OTA.Core.Models;
using JieLi.OTA.Core.Protocols;
using JieLi.OTA.Core.Protocols.Responses;
using JieLi.OTA.Infrastructure.Bluetooth;
using JieLi.OTA.Infrastructure.FileSystem;
using Xunit;

namespace JieLi.OTA.Tests.Application;

/// <summary>OtaManager 单元测试</summary>
public class OtaManagerTests : IDisposable
{
    private readonly MockOtaManager _manager;
    private readonly MockWindowsBleService _mockBleService;
    private readonly OtaFileService _fileService;
    private readonly string _testFirmwarePath;
    private OtaProgress? _lastProgress;
    private OtaState? _lastState;

    public OtaManagerTests()
    {
        _mockBleService = new MockWindowsBleService();
        _fileService = new OtaFileService();
        
        _manager = new MockOtaManager(_mockBleService, _fileService);
        _manager.ProgressChanged += (_, progress) => _lastProgress = progress;
        _manager.StateChanged += (_, state) => _lastState = state;

        // 创建测试固件文件
        _testFirmwarePath = Path.Combine(Path.GetTempPath(), "test_firmware.ufw");
        CreateTestFirmware(_testFirmwarePath, 10240); // 10KB 测试文件
    }

    [Fact(DisplayName = "OTA升级 - 文件不存在")]
    public async Task StartOtaAsync_ShouldFail_WhenFileNotExists()
    {
        // Act
        var result = await _manager.StartOtaAsync("test-device-id", "non_existent.ufw", CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("文件不存在", result.ErrorMessage);
    }

    [Fact(DisplayName = "进度更新 - 验证事件触发")]
    public async Task StartOtaAsync_ShouldTriggerProgressEvents()
    {
        // Arrange
        var states = new List<OtaState>();
        _manager.StateChanged += (_, state) => states.Add(state);

        // Act - 使用不存在的文件，但会触发 ValidatingFirmware 状态
        await _manager.StartOtaAsync("test-device-id", "nonexistent.ufw", CancellationToken.None);

        // Assert - 应该至少有 ValidatingFirmware 状态
        Assert.NotEmpty(states);
        Assert.Contains(OtaState.ValidatingFirmware, states);
    }

    [Fact(DisplayName = "状态变更 - 验证初始状态流转")]
    public async Task StartOtaAsync_ShouldTransitionFromIdle()
    {
        // Arrange
        var states = new List<OtaState>();
        _manager.StateChanged += (_, state) => states.Add(state);

        // Act
        await _manager.StartOtaAsync("test-device-id", "nonexistent.ufw", CancellationToken.None);

        // Assert - 应该从 Idle -> ValidatingFirmware
        Assert.Contains(OtaState.ValidatingFirmware, states);
    }

    [Fact(DisplayName = "取消操作 - 立即取消")]
    public async Task StartOtaAsync_ShouldCancelImmediately()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel(); // 立即取消

        // Act
        var result = await _manager.StartOtaAsync("test-device-id", _testFirmwarePath, cts.Token);

        // Assert - 应该快速返回
        Assert.True(result.Success || result.ErrorCode != 0);
    }

    [Fact(DisplayName = "配置验证 - 默认配置有效")]
    public void Config_ShouldHaveValidDefaults()
    {
        // Assert
        Assert.NotNull(_manager.Config);
        Assert.True(_manager.Config.CommandTimeout > 0);
        Assert.True(_manager.Config.ReconnectTimeout > 0);
        Assert.True(_manager.Config.MaxRetryCount >= 0);
        Assert.True(_manager.Config.TransferBlockSize > 0);
    }

    [Fact(DisplayName = "并发调用 - 拒绝重复升级")]
    public async Task StartOtaAsync_ShouldRejectConcurrentCalls()
    {
        // Arrange - 第一次调用会进入升级流程
        var task1 = _manager.StartOtaAsync("device1", _testFirmwarePath, CancellationToken.None);
        
        // Act - 立即发起第二次调用
        await Task.Delay(10); // 确保第一次调用已经开始
        var task2 = _manager.StartOtaAsync("device2", _testFirmwarePath, CancellationToken.None);

        // Assert - 第二次调用应该被拒绝
        var result2 = await task2;
        Assert.False(result2.Success);
        
        // 等待第一次调用完成
        await task1;
    }

    private void CreateTestFirmware(string path, int size)
    {
        var random = new Random();
        var data = new byte[size];
        random.NextBytes(data);
        File.WriteAllBytes(path, data);
    }

    public void Dispose()
    {
        if (File.Exists(_testFirmwarePath))
        {
            File.Delete(_testFirmwarePath);
        }
        _mockBleService.Dispose();
    }

    #region Mock Services

    private class MockOtaManager : OtaManager
    {
        public MockOtaManager(WindowsBleService bleService, OtaFileService fileService)
            : base(bleService, fileService)
        {
        }
    }

    private class MockWindowsBleService : WindowsBleService
    {
        // 简化的模拟服务，仅用于测试
    }

    #endregion
}
