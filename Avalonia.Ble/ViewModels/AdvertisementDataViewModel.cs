using Avalonia.Ble.Services;
using Avalonia.Controls;
using Avalonia.Input; // Required for PointerPressedEventArgs
using Avalonia.Interactivity; // Required for RoutedEventArgs

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using NewLife; // 用于 IsNullOrWhiteSpace
using NewLife.Log; // 用于 XTrace

using System.Collections.ObjectModel;
using System.Threading.Tasks;

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

    public IToastNotificationService ToastService { get; }

    /// <summary>
    /// 初始化 AdvertisementDataViewModel 类的新实例。
    /// </summary>
    public AdvertisementDataViewModel()
    {
        _rawData = "Design-time raw data";
        _advertisementData = new ObservableCollection<BleAdvertisementData>();
        ToastService = new ToastNotificationService(); // 初始化服务
    }

    /// <summary>
    /// 初始化 AdvertisementDataViewModel 类的新实例。
    /// </summary>
    /// <param name="device">设备信息。</param>
    public AdvertisementDataViewModel(BleDeviceInfo device)
    {
        Device = device;
        RawData = device.RawAdvertisementData;
        ToastService = new ToastNotificationService(); // 初始化服务

        AdvertisementData.Clear();
        foreach (var data in device.AdvertisementData)
        {
            AdvertisementData.Add(data);
        }
    }

    [RelayCommand]
    private async Task CopyRawDataAsync(TopLevel? topLevel)
    {
        if (RawData.IsNullOrWhiteSpace())
        {
            await ToastService.ShowToastAsync("没有可复制的内容。");
            return;
        }

        if (topLevel?.Clipboard is { } clipboardService)
        {
            try
            {
                await clipboardService.SetTextAsync(RawData);
                await ToastService.ShowToastAsync("原始数据已复制到剪贴板！");
            }
            catch (System.Exception ex)
            {
                XTrace.WriteException(ex);
                await ToastService.ShowToastAsync($"复制失败: {ex.Message}");
            }
        }
        else
        {
            if (topLevel == null)
            {
                XTrace.WriteLine("CopyRawDataAsync: TopLevel is null.");
            }
            else
            {
                XTrace.WriteLine("CopyRawDataAsync: topLevel.Clipboard is null.");
            }
            await ToastService.ShowToastAsync("无法访问剪贴板服务。");
        }
    }

    [RelayCommand]
    private async Task CopyValueAsync((string? Text, TopLevel? Window) data)
    {
        if (string.IsNullOrWhiteSpace(data.Text))
        {
            await ToastService.ShowToastAsync("没有可复制的内容。");
            return;
        }

        if (data.Window?.Clipboard is { } clipboardService)
        {
            try
            {
                await clipboardService.SetTextAsync(data.Text);
                await ToastService.ShowToastAsync("数据值已复制到剪贴板！");
            }
            catch (System.Exception ex)
            {
                XTrace.WriteException(ex);
                await ToastService.ShowToastAsync($"复制失败: {ex.Message}");
            }
        }
        else
        {
            XTrace.WriteLine("CopyValueAsync: Could not access ClipboardService. TopLevel or Clipboard might be null.");
            await ToastService.ShowToastAsync("无法访问剪贴板服务。");
        }
    }
}
