using Avalonia.Ble.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

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
}
