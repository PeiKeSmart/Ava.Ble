using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using JieLi.OTA.Application.Services;
using JieLi.OTA.Desktop.ViewModels;
using JieLi.OTA.Infrastructure.Bluetooth;
using JieLi.OTA.Infrastructure.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using AvaloniaApplication = Avalonia.Application;

namespace JieLi.OTA.Desktop;

public class App : AvaloniaApplication
{
    /// <summary>服务提供程序。</summary>
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 配置服务
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new Views.MainWindow
            {
                DataContext = Services.GetRequiredService<OtaUpgradeViewModel>()
            };
            
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>配置依赖注入服务。</summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // 注册基础服务
        services.AddSingleton<WindowsBleService>();
        services.AddSingleton<OtaFileService>();
        services.AddSingleton<RcspProtocol>();
        services.AddSingleton<ReconnectService>();
        services.AddSingleton<OtaManager>();

        // 注册 ViewModels
        services.AddTransient<OtaUpgradeViewModel>();
    }
}
