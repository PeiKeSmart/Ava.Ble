using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Avalonia.Ble.ViewModels;

public partial class MainWindowViewModel : ViewModelBase // Changed from ViewModelBase
{
    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusMessage = "请点击开始扫描按钮";

    [ObservableProperty]
    private ObservableCollection<object> _discoveredDevices = []; // 使用 object 作为占位符

    [ObservableProperty]
    private object? _selectedDevice;

    [RelayCommand]
    private void StartScan()
    {
        IsScanning = true;
        StatusMessage = "正在扫描设备...";
        // 在这里添加开始扫描的逻辑
        // 例如: DiscoveredDevices.Add(new { Name = "Test Device", Id = "123", SignalStrength = -50, LastSeenTime = DateTime.Now, IsConnectable = true });
    }

    [RelayCommand(CanExecute = nameof(CanStopScan))]
    private void StopScan()
    {
        IsScanning = false;
        StatusMessage = "扫描已停止。";
        // 在这里添加停止扫描的逻辑
    }

    private bool CanStopScan() => IsScanning;

    // 当 IsScanning 属性改变时，通知 StopScanCommand 的 CanExecute 状态也可能改变
    partial void OnIsScanningChanged(bool value)
    {
        StopScanCommand.NotifyCanExecuteChanged();
    }
}
