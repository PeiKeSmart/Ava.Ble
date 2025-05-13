using Avalonia.Ble.Services;
using Avalonia.Controls;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using NewLife; // 新增：用于 Debug 和 IsNullOrWhiteSpace
using NewLife.Log; // 用于 XTrace

using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Threading; // 用于 CancellationTokenSource

namespace Avalonia.Ble.ViewModels;

/// <summary>
/// 广播数据视图模型。
/// </summary>
public partial class AdvertisementDataViewModel : ViewModelBase
{
    [ObservableProperty]
    private BleDeviceInfo? _device;

    [ObservableProperty]
    private string _rawData = string.Empty;

    [ObservableProperty]
    private ObservableCollection<BleAdvertisementData> _advertisementData = [];

    [ObservableProperty]
    private string? _toastMessage;

    [ObservableProperty]
    private bool _isToastVisible;

    private CancellationTokenSource? _toastCts;

    /// <summary>
    /// 初始化 AdvertisementDataViewModel 类的新实例。
    /// </summary>
    public AdvertisementDataViewModel()
    {
        // Initialize properties if necessary for design-time data or default state
        _rawData = "Design-time raw data";
        _advertisementData = new ObservableCollection<BleAdvertisementData>();
    }

    /// <summary>
    /// 初始化 AdvertisementDataViewModel 类的新实例。
    /// </summary>
    /// <param name="device">设备信息。</param>
    public AdvertisementDataViewModel(BleDeviceInfo device)
    {
        Device = device;
        RawData = device.RawAdvertisementData;

        AdvertisementData.Clear();
        foreach (var data in device.AdvertisementData)
        {
            AdvertisementData.Add(data);
        }
    }

    private async Task ShowToastAsync(string message)
    {
        _toastCts?.Cancel(); // 取消任何正在进行的toast计时器
        _toastCts = new CancellationTokenSource();

        ToastMessage = message;
        IsToastVisible = true;

        try
        {
            await Task.Delay(3000, _toastCts.Token); // 等待3秒
            IsToastVisible = false;
        }
        catch (TaskCanceledException)
        {
            // 如果任务被取消（因为新的toast消息出现），则什么也不做
        }
        finally
        {
            // 确保在正常完成或未被取消时，toast是隐藏的
            if (_toastCts != null && !_toastCts.IsCancellationRequested)
            {
                IsToastVisible = false;
            }
            _toastCts?.Dispose();
            _toastCts = null;
        }
    }

    [RelayCommand]
    private async Task CopyRawDataAsync(TopLevel? topLevel)
    {
        if (RawData.IsNullOrWhiteSpace())
        {
            await ShowToastAsync("没有可复制的内容。");
            return;
        }

        if (topLevel?.Clipboard is { } clipboardService)
        {
            try
            {
                await clipboardService.SetTextAsync(RawData);
                await ShowToastAsync("原始数据已复制到剪贴板！");
            }
            catch (System.Exception ex)
            {
                XTrace.WriteException(ex);
                await ShowToastAsync($"复制失败: {ex.Message}");
            }
        }
        else
        {
            // 在实际应用中，这里可能也需要记录日志或向用户显示更具体的错误
            if (topLevel == null)
            {
                XTrace.WriteLine("CopyRawDataAsync: TopLevel is null.");
            }
            else
            {
                XTrace.WriteLine("CopyRawDataAsync: topLevel.Clipboard is null.");
            }
            await ShowToastAsync("无法访问剪贴板服务。");
        }
    }
}
