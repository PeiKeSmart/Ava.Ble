using Avalonia;

using System;

namespace Avalonia.Ble;
internal sealed class Program {
    // 初始化代码。在 AppMain 被调用之前，不要使用任何 Avalonia、第三方 API 或任何
    // 依赖 SynchronizationContext 的代码：因为它们还没有初始化，可能会导致问题。
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia 配置，不要移除；可视化设计器也会使用。
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
