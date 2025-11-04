using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JieLi.OTA.Application.Services;
using JieLi.OTA.Core.Models;
using JieLi.OTA.Infrastructure.Bluetooth;

namespace JieLi.OTA.Desktop.ViewModels;

/// <summary>
/// OTA 升级页面 ViewModel。
/// </summary>
public partial class OtaUpgradeViewModel : ViewModelBase
{
    private readonly WindowsBleService _bleService;
    private readonly OtaManager _otaManager;
    private CancellationTokenSource? _upgradeCts;

    [ObservableProperty]
    private ObservableCollection<BleDevice> _devices = [];

    [ObservableProperty]
    private BleDevice? _selectedDevice;

    [ObservableProperty]
    private string _firmwareFilePath = string.Empty;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isUpgrading;

    [ObservableProperty]
    private OtaState _currentState;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private double _transferSpeed;

    [ObservableProperty]
    private int _totalBytes;

    [ObservableProperty]
    private int _transferredBytes;

    /// <summary>传输进度文本</summary>
    public string TransferProgressText => $"{TransferredBytes} / {TotalBytes} bytes";

    [ObservableProperty]
    private string _deviceInfo = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _logs = [];

    /// <summary>初始化 OtaUpgradeViewModel。</summary>
    public OtaUpgradeViewModel(WindowsBleService bleService, OtaManager otaManager)
    {
        _bleService = bleService;
        _otaManager = otaManager;

        _bleService.DeviceDiscovered += OnDeviceDiscovered;
        _otaManager.StateChanged += OnStateChanged;
        _otaManager.ProgressChanged += OnProgressChanged;
    }

    [RelayCommand]
    private void StartScan()
    {
        Devices.Clear();
        IsScanning = true;
        StatusMessage = "扫描中...";
        AddLog("开始扫描 BLE 设备...");

        _bleService.StartScan();

        // 5秒后自动停止扫描
        Task.Delay(5000).ContinueWith(_ =>
        {
            StopScan();
        });
    }

    [RelayCommand]
    private void StopScan()
    {
        _bleService.StopScan();
        IsScanning = false;
        StatusMessage = $"扫描完成,发现 {Devices.Count} 个设备";
        AddLog($"扫描停止,共发现 {Devices.Count} 个设备");
    }

    [RelayCommand]
    private async Task SelectFirmwareFileAsync()
    {
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择固件文件",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("固件文件") { Patterns = ["*.ufw", "*.bin"] }]
        });

        if (files.Count > 0)
        {
            FirmwareFilePath = files[0].Path.LocalPath;
            AddLog($"已选择固件文件: {FirmwareFilePath}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartUpgrade))]
    private async Task StartUpgradeAsync()
    {
        if (SelectedDevice == null || string.IsNullOrEmpty(FirmwareFilePath)) return;

        IsUpgrading = true;
        Progress = 0;
        StatusMessage = "准备升级...";
        AddLog("--- 开始 OTA 升级 ---");
        AddLog($"设备: {SelectedDevice.DeviceName} ({SelectedDevice.DeviceId})");
        AddLog($"固件: {FirmwareFilePath}");

        _upgradeCts = new CancellationTokenSource();

        try
        {
            var result = await _otaManager.StartOtaAsync(
                SelectedDevice.DeviceId,
                FirmwareFilePath,
                _upgradeCts.Token);

            if (result.Success)
            {
                StatusMessage = "升级成功!";
                AddLog("✓ OTA 升级成功完成!");
            }
            else
            {
                StatusMessage = $"升级失败: {result.ErrorMessage}";
                AddLog($"✗ OTA 升级失败: {result.ErrorMessage}");
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "升级已取消";
            AddLog("用户取消了 OTA 升级");
        }
        catch (Exception ex)
        {
            StatusMessage = $"升级异常: {ex.Message}";
            AddLog($"✗ 异常: {ex.Message}");
        }
        finally
        {
            IsUpgrading = false;
            _upgradeCts?.Dispose();
            _upgradeCts = null;
        }
    }

    private bool CanStartUpgrade() => SelectedDevice != null && !string.IsNullOrEmpty(FirmwareFilePath) && !IsUpgrading;

    [RelayCommand(CanExecute = nameof(CanCancelUpgrade))]
    private void CancelUpgrade()
    {
        _upgradeCts?.Cancel();
        AddLog("正在取消升级...");
    }

    private bool CanCancelUpgrade() => IsUpgrading;

    [RelayCommand]
    private void ClearLogs()
    {
        Logs.Clear();
    }

    private void OnDeviceDiscovered(object? sender, BleDevice device)
    {
        if (!Devices.Any(d => d.DeviceId == device.DeviceId))
        {
            Devices.Add(device);
            AddLog($"发现设备: {device.DeviceName} (RSSI: {device.Rssi})");
        }
    }

    private void OnStateChanged(object? sender, OtaState state)
    {
        CurrentState = state;
        StatusMessage = GetStateMessage(state);
        AddLog($"状态变更: {StatusMessage}");
    }

    private void OnProgressChanged(object? sender, OtaProgress progress)
    {
        TotalBytes = (int)progress.TotalBytes;
        TransferredBytes = (int)progress.TransferredBytes;
        TransferSpeed = progress.Speed;

        // 通知计算属性更新
        OnPropertyChanged(nameof(TransferProgressText));

        if (progress.TotalBytes > 0)
        {
            Progress = (int)((double)progress.TransferredBytes / progress.TotalBytes * 100);
        }

        var speedText = FormatSpeed(progress.Speed);
        StatusMessage = $"{GetStateMessage(progress.State)} - {Progress}% ({speedText})";
    }

    private void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Logs.Add($"[{timestamp}] {message}");

        // 限制日志数量
        while (Logs.Count > 100)
        {
            Logs.RemoveAt(0);
        }
    }

    private static string GetStateMessage(OtaState state) => state switch
    {
        OtaState.Idle => "空闲",
        OtaState.Connecting => "连接设备中...",
        OtaState.GettingDeviceInfo => "获取设备信息...",
        OtaState.ReadingFileOffset => "读取文件偏移...",
        OtaState.ValidatingFirmware => "验证固件...",
        OtaState.EnteringUpdateMode => "进入升级模式...",
        OtaState.WaitingReconnect => "等待设备重连...",
        OtaState.TransferringFile => "传输固件数据...",
        OtaState.QueryingResult => "查询升级结果...",
        OtaState.Rebooting => "设备重启中...",
        OtaState.Completed => "升级完成",
        OtaState.Failed => "升级失败",
        OtaState.Cancelled => "已取消",
        _ => state.ToString()
    };

    private static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024)
            return $"{bytesPerSecond:F1} B/s";
        if (bytesPerSecond < 1024 * 1024)
            return $"{bytesPerSecond / 1024:F1} KB/s";
        return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bleService.DeviceDiscovered -= OnDeviceDiscovered;
            _otaManager.StateChanged -= OnStateChanged;
            _otaManager.ProgressChanged -= OnProgressChanged;
            _upgradeCts?.Dispose();
        }
        base.Dispose(disposing);
    }
}
