using CommunityToolkit.Mvvm.ComponentModel;

namespace JieLi.OTA.Desktop.ViewModels;

/// <summary>
/// ViewModel 基类,提供通知功能和资源释放支持。
/// </summary>
public class ViewModelBase : ObservableObject, IDisposable
{
    private bool _disposed;

    /// <summary>释放资源。</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>释放托管和非托管资源。</summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
    }
}
