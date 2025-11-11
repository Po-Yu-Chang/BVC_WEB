using System.Windows.Data;
using System.Windows.Markup;
using MesMiddleware.Monitor.Services;

namespace MesMiddleware.Monitor.Helpers;

/// <summary>
/// XAML 標記擴展，用於綁定本地化資源
/// 使用方式：{local:Localize WindowTitle}
/// </summary>
[MarkupExtensionReturnType(typeof(BindingExpression))]
public class LocalizeExtension : MarkupExtension
{
    /// <summary>
    /// 資源鍵值
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// 建構函式
    /// </summary>
    /// <param name="key">資源鍵值</param>
    public LocalizeExtension(string key)
    {
        Key = key;
    }

    /// <summary>
    /// 提供值給 XAML
    /// </summary>
    /// <param name="serviceProvider">服務提供者</param>
    /// <returns>綁定表達式</returns>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // 建立綁定到 LocalizationService 的 GetString 方法
        var binding = new Binding($"LocalizationService[{Key}]")
        {
            Mode = BindingMode.OneWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };

        return binding.ProvideValue(serviceProvider);
    }
}
