using Avalonia.Ble.Services;
using Avalonia.Ble.ViewModels;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Avalonia.Interactivity;

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

    private async void OnCopyableTextPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && control.Tag is string textToCopy)
        {
            if (DataContext is AdvertisementDataViewModel viewModel)
            {
                var topLevel = TopLevel.GetTopLevel(control);
                if (topLevel != null)
                {
                    await viewModel.CopyValueCommand.ExecuteAsync((textToCopy, topLevel));
                }
                else
                {
                    // Handle case where TopLevel can't be found, though unlikely for a control in a window.
                    // viewModel.ToastService.ShowToastAsync("无法确定窗口上下文以进行复制。"); // Example
                }
            }
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
