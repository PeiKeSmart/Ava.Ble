using Avalonia.Ble.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

using System;

namespace Avalonia.Ble;
public class ViewLocator : IDataTemplate {

    /// <summary>
    /// 根据提供的参数构建控件。
    /// </summary>
    /// <param name="param">用于构建控件的参数，通常是 ViewModel。</param>
    /// <returns>构建的控件，如果无法创建则返回 null 或 TextBlock 错误信息。</returns>
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "未找到: " + name };
    }

    /// <summary>
    /// 确定此数据模板是否可以处理提供的数据。
    /// </summary>
    /// <param name="data">要检查的数据对象。</param>
    /// <returns>如果数据是 ViewModelBase 的实例，则为 true；否则为 false。</returns>
    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
