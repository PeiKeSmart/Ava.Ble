using System.ComponentModel;
using System.Threading.Tasks;

namespace Avalonia.Ble.Services;

public interface IToastNotificationService : INotifyPropertyChanged
{
    string? ToastMessage { get; }
    bool IsToastVisible { get; }
    Task ShowToastAsync(string message, int duration = 3000);
}
