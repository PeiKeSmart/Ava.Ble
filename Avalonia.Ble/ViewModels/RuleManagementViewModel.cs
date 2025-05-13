using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading.Tasks;
// 如果您打算使用 Avalonia 的 StorageProvider 来进行文件操作，请取消注释以下 using
// using Avalonia.Platform.Storage;

namespace Avalonia.Ble.ViewModels
{
    public partial class RuleManagementViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _ruleText = string.Empty;

        // 规则文件的建议路径和名称
        private const string RulesFilePath = "ble_filter_rules.txt"; // 您可以根据需要更改此路径

        public RuleManagementViewModel()
        {
            LoadRulesCommand = new AsyncRelayCommand(LoadRulesAsync);
            SaveRulesCommand = new AsyncRelayCommand(SaveRulesAsync);

            // 可选：在 ViewModel 初始化时自动加载规则
            // Task.Run(LoadRulesAsync); 
        }

        public IAsyncRelayCommand LoadRulesCommand { get; }
        public IAsyncRelayCommand SaveRulesCommand { get; }

        private async Task LoadRulesAsync()
        {
            try
            {
                // 简单的文件读取逻辑
                // TODO: 考虑使用 Avalonia 的 StorageProvider API 来提供文件选择对话框
                if (File.Exists(RulesFilePath))
                {
                    RuleText = await File.ReadAllTextAsync(RulesFilePath);
                }
                else
                {
                    RuleText = "{\n  \"rules\": [\n    {\n      \"property\": \"Name\",\n      \"operator\": \"Contains\",\n      \"value\": \"MyDevice\"\n    },\n    {\n      \"property\": \"Rssi\",\n      \"operator\": \">\",\n      \"value\": -70\n    }\n  ]\n}"; // 提供一个默认的规则示例
                }
            }
            catch (Exception ex)
            {
                // TODO: 处理异常，例如通过状态消息或日志显示错误
                Console.WriteLine($"Error loading rules: {ex.Message}");
                RuleText = $"// Error loading rules: {ex.Message}";
            }
        }

        private async Task SaveRulesAsync()
        {
            try
            {
                // 简单的文件保存逻辑
                // TODO: 考虑使用 Avalonia 的 StorageProvider API 来提供文件保存对话框
                await File.WriteAllTextAsync(RulesFilePath, RuleText);
                // TODO: 可以添加保存成功的提示
                Console.WriteLine($"Rules saved to {RulesFilePath}");
            }
            catch (Exception ex)
            {
                // TODO: 处理异常
                Console.WriteLine($"Error saving rules: {ex.Message}");
            }
        }

        // 此方法用于从 MainWindowViewModel 获取规则，以便在 ApplyFilter 中使用
        // 您可以根据规则的实际格式（例如 JSON、XML 或自定义格式）来解析 RuleText
        public string GetCurrentRules()
        {
            return RuleText;
        }
    }
}
