using Avalonia.Ble.Services;
using Avalonia.Ble.ViewModels;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Avalonia.Ble.Views;

public partial class AdvertisementDataWindow : Window
{
    public AdvertisementDataWindow()
    {
        InitializeComponent();
    }

    public AdvertisementDataWindow(BleDeviceInfo device)
    {
        InitializeComponent();
        DataContext = new AdvertisementDataViewModel(device);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
