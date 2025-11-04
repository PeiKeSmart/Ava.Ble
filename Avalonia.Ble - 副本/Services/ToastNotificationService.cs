using Avalonia.Ble.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Ble.Services;

public partial class ToastNotificationService : ObservableObject, IToastNotificationService
{
    [ObservableProperty]
    private string? _toastMessage;

    [ObservableProperty]
    private bool _isToastVisible;

    private CancellationTokenSource? _toastCts;

    public async Task ShowToastAsync(string message, int duration = 3000)
    {
        _toastCts?.Cancel(); // Cancel any ongoing toast timer
        _toastCts?.Dispose();
        _toastCts = new CancellationTokenSource();

        ToastMessage = message;
        IsToastVisible = true;

        try
        {
            await Task.Delay(duration, _toastCts.Token);
            IsToastVisible = false;
        }
        catch (TaskCanceledException)
        {
            // If the task was canceled (because a new toast message appeared or disposed),
            // IsToastVisible might have already been set by the new call or should remain as is.
        }
        finally
        {
            // Ensure that if the delay completed normally and wasn't cancelled by a new toast,
            // the toast becomes invisible.
            if (_toastCts != null && !_toastCts.IsCancellationRequested)
            {
                IsToastVisible = false;
            }
            // Don't dispose _toastCts here if it might be the CTS of a newer toast.
            // It's disposed at the beginning of the method or when a new one is created.
        }
    }
}
