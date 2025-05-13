using Avalonia;
using Avalonia.Ble.ViewModels;
using Avalonia.Ble.Views;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;

using System.Linq;

namespace Avalonia.Ble;
public partial class App : Application {
    /// <summary>
    /// 初始化应用程序。
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// 在框架初始化完成后调用。
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 避免 Avalonia 和 CommunityToolkit 的重复验证。
            // 更多信息: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 禁用 Avalonia 数据注解验证。
    /// </summary>
    private void DisableAvaloniaDataAnnotationValidation()
    {
        // 获取要移除的插件数组
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // 移除找到的每个条目
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}