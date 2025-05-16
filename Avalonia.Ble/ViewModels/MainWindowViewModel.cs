using Avalonia.Ble.Services;
using Avalonia.Ble.Views;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Avalonia.Ble.ViewModels;

/// <summary>
/// 主窗口的视图模型。
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly BleService _bleService;
    private RuleManagementViewModel? _ruleManagementViewModel;

    // 保存DataGrid的滚动位置
    private double _scrollPosition = 0;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusMessage = "请点击开始扫描按钮";

    private ObservableCollection<BleDeviceInfo> _discoveredDevices = [];

    /// <summary>
    /// 获取或设置过滤后的设备列表。
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<BleDeviceInfo> _filteredDevices = [];

    /// <summary>
    /// 获取或设置设备名称过滤文本。
    /// </summary>
    [ObservableProperty]
    private string _deviceNameFilter = string.Empty;

    /// <summary>
    /// 获取或设置是否启用过滤。
    /// </summary>
    [ObservableProperty]
    private bool _isFilterEnabled = false;

    /// <summary>
    /// 获取或设置设备超时时间（秒）。超过此时间未收到广播的设备将被自动移除。
    /// 设置为0表示不自动清理设备。
    /// </summary>
    [ObservableProperty]
    private int _deviceTimeoutSeconds = 0;

    /// <summary>
    /// 获取过滤按钮文本。
    /// </summary>
    public string FilterButtonText => IsFilterEnabled ? "关闭过滤" : "开启过滤";

    [ObservableProperty]
    private BleDeviceInfo? _selectedDevice;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private ObservableCollection<BleServiceInfo> _selectedDeviceServices = [];

    [ObservableProperty]
    private BleServiceInfo? _selectedService;

    [ObservableProperty]
    private ObservableCollection<BleCharacteristicInfo> _selectedServiceCharacteristics = [];

    [ObservableProperty]
    private BleCharacteristicInfo? _selectedCharacteristic;

    /// <summary>
    /// 初始化 MainWindowViewModel 类的新实例。
    /// </summary>
    public MainWindowViewModel()
    {
        _bleService = new BleService();
        _bleService.DeviceDiscovered += OnDeviceDiscovered;
        _bleService.ScanStatusChanged += OnScanStatusChanged;
        _bleService.ErrorOccurred += OnErrorOccurred;
        _bleService.DeviceConnected += OnDeviceConnected;
        _bleService.DeviceDisconnected += OnDeviceDisconnected;
        _bleService.ServiceDiscovered += OnServiceDiscovered;
        _bleService.DeviceRemoved += OnDeviceRemoved;

        // 初始化过滤后的设备列表
        ApplyFilter();
    }

    [RelayCommand]
    private void OpenRuleManagement()
    {
        _ruleManagementViewModel = new RuleManagementViewModel();
        var ruleManagementWindow = new RuleManagementWindow
        {
            DataContext = _ruleManagementViewModel
        };
        ruleManagementWindow.Show();

        ruleManagementWindow.Closed += (s, e) => ApplyFilter();
    }

    /// <summary>
    /// 当选定设备更改时调用。
    /// </summary>
    /// <param name="value">新的选定设备。</param>
    partial void OnSelectedDeviceChanged(BleDeviceInfo? value)
    {
        if (value != null)
        {
            SelectedDeviceServices.Clear();
            foreach (var service in value.Services)
            {
                SelectedDeviceServices.Add(service);
            }
        }
        else
        {
            SelectedDeviceServices.Clear();
        }

        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 当选定服务更改时调用。
    /// </summary>
    /// <param name="value">新的选定服务。</param>
    partial void OnSelectedServiceChanged(BleServiceInfo? value)
    {
        if (value != null)
        {
            SelectedServiceCharacteristics.Clear();
            foreach (var characteristic in value.Characteristics)
            {
                SelectedServiceCharacteristics.Add(characteristic);
            }
        }
        else
        {
            SelectedServiceCharacteristics.Clear();
        }
    }

    /// <summary>
    /// 开始扫描 BLE 设备。
    /// </summary>
    [RelayCommand]
    private void StartScan()
    {
        StatusMessage = "正在扫描设备...";

        Dispatcher.UIThread.Post(() =>
        {
            SelectedDevice = null;

            lock (_discoveredDevices)
            {
                _discoveredDevices = [];
            }

            FilteredDevices = [];
        });

        _bleService.StartScan();
        IsScanning = true;
    }

    /// <summary>
    /// 停止扫描 BLE 设备。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStopScan))]
    private void StopScan()
    {
        StatusMessage = "扫描已停止。";
        _bleService.StopScan();
        IsScanning = false;
    }

    /// <summary>
    /// 确定是否可以停止扫描。
    /// </summary>
    /// <returns>如果正在扫描，则为 true；否则为 false。</returns>
    private bool CanStopScan() => IsScanning;

    /// <summary>
    /// 当 IsScanning 属性更改时调用。
    /// </summary>
    /// <param name="value">IsScanning 的新值。</param>
    partial void OnIsScanningChanged(bool value)
    {
        StopScanCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 当设备名称过滤文本更改时调用。
    /// </summary>
    /// <param name="value">新的过滤文本。</param>
    partial void OnDeviceNameFilterChanged(string value)
    {
        if (IsFilterEnabled)
        {
            ApplyFilter();
        }
    }

    /// <summary>
    /// 当是否启用过滤更改时调用。
    /// </summary>
    /// <param name="value">新的启用状态。</param>
    partial void OnIsFilterEnabledChanged(bool value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(FilterButtonText));
    }

    /// <summary>
    /// 当设备超时时间更改时调用。
    /// </summary>
    /// <param name="value">新的超时时间（秒）。</param>
    partial void OnDeviceTimeoutSecondsChanged(int value)
    {
        _bleService.DeviceTimeoutSeconds = value;
    }

    /// <summary>
    /// 处理设备移除事件。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="deviceId">被移除的设备ID。</param>
    private void OnDeviceRemoved(object? sender, string deviceId)
    {
        Dispatcher.UIThread.Post(() =>
        {
            lock (_discoveredDevices)
            {
                var deviceToRemove = _discoveredDevices.FirstOrDefault(d => d.Id == deviceId);
                if (deviceToRemove != null)
                {
                    _discoveredDevices.Remove(deviceToRemove);
                }
            }
            ApplyFilter();
        });
    }

    /// <summary>
    /// 应用设备过滤。
    /// </summary>
    private void ApplyFilter()
    {
        string? selectedDeviceId = SelectedDevice?.Id;

        List<BleDeviceInfo> sourceListForFilter;
        lock (_discoveredDevices)
        {
            sourceListForFilter = [.. _discoveredDevices];
        }

        List<BleDeviceInfo> devicesThatShouldBeInFilteredView = [];

        if (IsFilterEnabled)
        {
            bool rulesApplied = false;
            if (_ruleManagementViewModel != null)
            {
                string rulesJson = _ruleManagementViewModel.GetCurrentRules();
                if (!string.IsNullOrWhiteSpace(rulesJson))
                {
                    try
                    {
                        var parsedRules = JsonSerializer.Deserialize<RuleSet>(rulesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (parsedRules?.Rules != null && parsedRules.Rules.Any())
                        {
                            foreach (var device in sourceListForFilter)
                            {
                                if (MatchesAllRules(device, parsedRules.Rules))
                                {
                                    devicesThatShouldBeInFilteredView.Add(device);
                                }
                            }
                            rulesApplied = true;
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.WriteLine($"Error parsing rules JSON: {jsonEx.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error applying rules: {ex.Message}");
                    }
                }
            }

            if (!rulesApplied)
            {
                if (string.IsNullOrWhiteSpace(DeviceNameFilter))
                {
                    devicesThatShouldBeInFilteredView.AddRange(sourceListForFilter);
                }
                else
                {
                    string filterText = DeviceNameFilter.Trim();
                    foreach (var device in sourceListForFilter)
                    {
                        bool nameMatches = device.Name != null && device.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase);
                        bool isUnknownDeviceMatch = (string.IsNullOrEmpty(device.Name) || device.Name == "未知设备") &&
                                                    filterText.Equals("未知设备", StringComparison.OrdinalIgnoreCase);

                        if (nameMatches || isUnknownDeviceMatch)
                        {
                            devicesThatShouldBeInFilteredView.Add(device);
                        }
                    }
                }
            }
        }
        else
        {
            devicesThatShouldBeInFilteredView.AddRange(sourceListForFilter);
        }

        var itemsToRemove = FilteredDevices.Except(devicesThatShouldBeInFilteredView).ToList();
        foreach (var itemToRemove in itemsToRemove)
        {
            FilteredDevices.Remove(itemToRemove);
        }

        for (int i = 0; i < devicesThatShouldBeInFilteredView.Count; i++)
        {
            var itemFromSource = devicesThatShouldBeInFilteredView[i];
            if (i >= FilteredDevices.Count)
            {
                FilteredDevices.Add(itemFromSource);
            }
            else if (!ReferenceEquals(FilteredDevices[i], itemFromSource))
            {
                int existingIndexOfItemFromSource = -1;
                for (int j = 0; j < FilteredDevices.Count; ++j)
                {
                    if (ReferenceEquals(FilteredDevices[j], itemFromSource))
                    {
                        existingIndexOfItemFromSource = j;
                        break;
                    }
                }

                if (existingIndexOfItemFromSource != -1)
                {
                    FilteredDevices.Move(existingIndexOfItemFromSource, i);
                }
                else
                {
                    FilteredDevices.Insert(i, itemFromSource);
                }
            }
        }

        while (FilteredDevices.Count > devicesThatShouldBeInFilteredView.Count)
        {
            FilteredDevices.RemoveAt(FilteredDevices.Count - 1);
        }

        if (selectedDeviceId != null)
        {
            SelectedDevice = FilteredDevices.FirstOrDefault(d => d.Id == selectedDeviceId);
        }
        else
        {
            SelectedDevice = null;
        }
    }

    private bool MatchesAllRules(BleDeviceInfo device, IEnumerable<Rule> rules)
    {
        foreach (var rule in rules)
        {
            if (!MatchesRule(device, rule))
            {
                return false;
            }
        }
        return true;
    }

    private bool MatchesRule(BleDeviceInfo device, Rule rule)
    {
        string? propertyValue = null;
        switch (rule.Property?.ToLowerInvariant())
        {
            case "name":
                propertyValue = device.Name;
                break;
            case "id":
                propertyValue = device.Id;
                break;
            case "address":
                propertyValue = device.Address.ToString();
                break;
            case "version":
                propertyValue = device.Version;
                break;
            case "rssi":
                if (int.TryParse(rule.Value?.ToString(), out int ruleRssiValue))
                {
                    switch (rule.Operator?.ToLowerInvariant())
                    {
                        case ">": return device.Rssi > ruleRssiValue;
                        case "<": return device.Rssi < ruleRssiValue;
                        case ">=": return device.Rssi >= ruleRssiValue;
                        case "<=": return device.Rssi <= ruleRssiValue;
                        case "==": return device.Rssi == ruleRssiValue;
                        default: return false;
                    }
                }
                return false;
        }

        if (propertyValue == null && rule.Property?.ToLowerInvariant() != "name")
        {
            if (rule.Operator?.ToLowerInvariant() == "isnullorempty") return string.IsNullOrEmpty(propertyValue);
            if (rule.Operator?.ToLowerInvariant() == "isnotnullorempty") return !string.IsNullOrEmpty(propertyValue);
        }

        string ruleValueString = rule.Value?.ToString() ?? string.Empty;

        switch (rule.Operator?.ToLowerInvariant())
        {
            case "contains":
                return propertyValue?.Contains(ruleValueString, StringComparison.OrdinalIgnoreCase) ?? false;
            case "equals":
                return propertyValue?.Equals(ruleValueString, StringComparison.OrdinalIgnoreCase) ?? false;
            case "startswith":
                return propertyValue?.StartsWith(ruleValueString, StringComparison.OrdinalIgnoreCase) ?? false;
            case "endswith":
                return propertyValue?.EndsWith(ruleValueString, StringComparison.OrdinalIgnoreCase) ?? false;
            case "isnullorempty":
                return string.IsNullOrEmpty(propertyValue);
            case "isnotnullorempty":
                return !string.IsNullOrEmpty(propertyValue);
            default:
                return false;
        }
    }

    /// <summary>
    /// 处理 DeviceDiscovered 事件。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="deviceInfoFromEvent">发现的设备信息。</param>
    private void OnDeviceDiscovered(object? sender, BleDeviceInfo deviceInfoFromEvent)
    {
        Dispatcher.UIThread.Post(() =>
        {
            BleDeviceInfo? existingDeviceInMasterList;
            lock (_discoveredDevices)
            {
                existingDeviceInMasterList = _discoveredDevices.FirstOrDefault(d => d.Id == deviceInfoFromEvent.Id);

                if (existingDeviceInMasterList != null)
                {
                    string newNameFromEvent = deviceInfoFromEvent.Name;

                    if (!string.IsNullOrEmpty(newNameFromEvent) && newNameFromEvent != "未知设备")
                    {
                        if (existingDeviceInMasterList.Name != newNameFromEvent)
                        {
                            existingDeviceInMasterList.Name = newNameFromEvent;
                        }
                    }
                    else if (string.IsNullOrEmpty(existingDeviceInMasterList.Name) || existingDeviceInMasterList.Name == "未知设备")
                    {
                        if (existingDeviceInMasterList.Name != "未知设备")
                        {
                            existingDeviceInMasterList.Name = "未知设备";
                        }
                    }

                    existingDeviceInMasterList.Rssi = deviceInfoFromEvent.Rssi;
                    existingDeviceInMasterList.LastSeen = deviceInfoFromEvent.LastSeen;
                    existingDeviceInMasterList.IsConnectable = deviceInfoFromEvent.IsConnectable;
                    existingDeviceInMasterList.Version = deviceInfoFromEvent.Version;
                    existingDeviceInMasterList.AdvertisementData = new List<BleAdvertisementData>(deviceInfoFromEvent.AdvertisementData);
                    existingDeviceInMasterList.RawAdvertisementData = deviceInfoFromEvent.RawAdvertisementData;
                }
                else
                {
                    var newDeviceToAdd = new BleDeviceInfo
                    {
                        Id = deviceInfoFromEvent.Id,
                        Name = (!string.IsNullOrEmpty(deviceInfoFromEvent.Name) && deviceInfoFromEvent.Name != "未知设备") ? deviceInfoFromEvent.Name : "未知设备",
                        Address = deviceInfoFromEvent.Address,
                        Rssi = deviceInfoFromEvent.Rssi,
                        LastSeen = deviceInfoFromEvent.LastSeen,
                        IsConnectable = deviceInfoFromEvent.IsConnectable,
                        ServiceCount = deviceInfoFromEvent.ServiceCount,
                        IsConnected = deviceInfoFromEvent.IsConnected,
                        AdvertisementData = new List<BleAdvertisementData>(deviceInfoFromEvent.AdvertisementData),
                        RawAdvertisementData = deviceInfoFromEvent.RawAdvertisementData,
                        Version = deviceInfoFromEvent.Version
                    };
                    _discoveredDevices.Add(newDeviceToAdd);
                }
            }
            ApplyFilter();
        });
    }

    /// <summary>
    /// 处理 ScanStatusChanged 事件。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="status">新的扫描状态。</param>
    private void OnScanStatusChanged(object? sender, string status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = status;
        });
    }

    /// <summary>
    /// 处理 ErrorOccurred 事件。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="errorMessage">错误消息。</param>
    private void OnErrorOccurred(object? sender, string errorMessage)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = $"错误: {errorMessage}";
        });
    }

    /// <summary>
    /// 处理 DeviceConnected 事件。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="deviceInfo">连接的设备信息。</param>
    private void OnDeviceConnected(object? sender, BleDeviceInfo deviceInfo)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnecting = false;

            if (SelectedDevice != null && SelectedDevice.Id == deviceInfo.Id)
            {
                SelectedDeviceServices.Clear();
                foreach (var service in deviceInfo.Services)
                {
                    SelectedDeviceServices.Add(service);
                }
            }

            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        });
    }

    /// <summary>
    /// 处理 DeviceDisconnected 事件。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="deviceInfo">断开连接的设备信息。</param>
    private void OnDeviceDisconnected(object? sender, BleDeviceInfo deviceInfo)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (SelectedDevice != null && SelectedDevice.Id == deviceInfo.Id)
            {
                SelectedDeviceServices.Clear();
            }

            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        });
    }

    /// <summary>
    /// 处理 ServiceDiscovered 事件。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="serviceInfo">发现的服务信息。</param>
    private void OnServiceDiscovered(object? sender, BleServiceInfo serviceInfo)
    {
    }

    /// <summary>
    /// 异步连接到选定的 BLE 设备。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (SelectedDevice == null) return;

        try
        {
            IsConnecting = true;
            StatusMessage = $"正在连接到设备: {SelectedDevice.Name}...";

            bool result = await _bleService.ConnectToDeviceAsync(SelectedDevice);

            if (!result)
            {
                StatusMessage = $"连接设备失败: {SelectedDevice.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"连接时出错: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
        }
    }

    /// <summary>
    /// 确定是否可以连接到选定的设备。
    /// </summary>
    /// <returns>如果可以连接，则为 true；否则为 false。</returns>
    private bool CanConnect()
    {
        return SelectedDevice != null &&
               SelectedDevice.IsConnectable &&
               !SelectedDevice.IsConnected &&
               !IsConnecting;
    }

    /// <summary>
    /// 断开与选定 BLE 设备的连接。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private void Disconnect()
    {
        if (SelectedDevice == null || !SelectedDevice.IsConnected) return;

        _bleService.DisconnectDevice(SelectedDevice);
    }

    /// <summary>
    /// 确定是否可以断开与选定设备的连接。
    /// </summary>
    /// <returns>如果可以断开连接，则为 true；否则为 false。</returns>
    private bool CanDisconnect()
    {
        return SelectedDevice != null && SelectedDevice.IsConnected;
    }

    /// <summary>
    /// 切换过滤状态。
    /// </summary>
    [RelayCommand]
    private void ToggleFilter()
    {
        IsFilterEnabled = !IsFilterEnabled;
    }

    /// <summary>
    /// 保存DataGrid的滚动位置。
    /// </summary>
    /// <param name="scrollViewer">ScrollViewer控件。</param>
    public void SaveScrollPosition(object scrollViewer)
    {
        if (scrollViewer is Avalonia.Controls.ScrollViewer viewer)
        {
            _scrollPosition = viewer.Offset.Y;
        }
    }

    /// <summary>
    /// 恢复DataGrid的滚动位置。
    /// </summary>
    /// <param name="scrollViewer">ScrollViewer控件。</param>
    public void RestoreScrollPosition(object scrollViewer)
    {
        if (scrollViewer is Avalonia.Controls.ScrollViewer viewer)
        {
            viewer.Offset = new Avalonia.Vector(viewer.Offset.X, _scrollPosition);
        }
    }

    /// <summary>
    /// 查看设备的广播数据。
    /// </summary>
    /// <param name="parameter">设备参数，如果为null则使用当前选中的设备。</param>
    [RelayCommand]
    private void ViewAdvertisementData(object? parameter)
    {
        BleDeviceInfo? device = parameter as BleDeviceInfo ?? SelectedDevice;
        if (device == null) return;

        if (device.AdvertisementData == null || device.AdvertisementData.Count == 0)
        {
            StatusMessage = "该设备没有广播数据";
            return;
        }

        var window = new AdvertisementDataWindow(device);
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            window.ShowDialog(desktop.MainWindow!);
        }
    }
}

public class RuleSet
{
    public List<Rule>? Rules { get; set; }
}

public class Rule
{
    public string? Property { get; set; }
    public string? Operator { get; set; }
    public object? Value { get; set; }
}
