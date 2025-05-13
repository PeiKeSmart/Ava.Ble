using Avalonia.Ble.Services;
using Avalonia.Controls;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using NewLife; // 新增：用于 Debug
using NewLife.Log;

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

    [RelayCommand]
    private async Task CopyRawDataAsync(TopLevel? topLevel)
    {
        if (RawData.IsNullOrWhiteSpace())
        {
            return;
        }

        if (topLevel?.Clipboard is { } clipboardService)
        {
            try
            {
                await clipboardService.SetTextAsync(RawData);
            }
            catch (System.Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }
        else
        {
            if (topLevel == null)
            {
            }
            else
            {
            }
        }
    }
}
