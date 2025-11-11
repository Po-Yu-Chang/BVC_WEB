using System.Globalization;
using System.Resources;
using System.Windows;

namespace MesMiddleware.Monitor.Services;

/// <summary>
/// 本地化服務實作
/// 管理應用程式的多語言支援
/// </summary>
public class LocalizationService : ILocalizationService
{
    private CultureInfo _currentCulture;
    private readonly ResourceManager _resourceManager;

    /// <summary>
    /// 當前文化資訊
    /// </summary>
    public CultureInfo CurrentCulture => _currentCulture;

    /// <summary>
    /// 語言變更事件
    /// </summary>
    public event EventHandler? LanguageChanged;

    /// <summary>
    /// 建構函式，初始化本地化服務
    /// </summary>
    public LocalizationService()
    {
        // 初始化 ResourceManager，指向 Resources.Strings 資源
        _resourceManager = new ResourceManager(
            "MesMiddleware.Monitor.Resources.Strings",
            typeof(LocalizationService).Assembly);

        // 預設使用繁體中文
        _currentCulture = new CultureInfo("zh-TW");
        ApplyCulture(_currentCulture);
    }

    /// <summary>
    /// 變更語言
    /// </summary>
    /// <param name="cultureName">文化名稱（例如：en, zh-CN, zh-TW）</param>
    public void ChangeLanguage(string cultureName)
    {
        var newCulture = new CultureInfo(cultureName);
        if (_currentCulture.Name == newCulture.Name)
        {
            return; // 語言相同，不需要變更
        }

        _currentCulture = newCulture;
        ApplyCulture(newCulture);

        // 觸發語言變更事件
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 取得本地化字串
    /// </summary>
    /// <param name="key">資源鍵值</param>
    /// <returns>本地化後的字串</returns>
    public string GetString(string key)
    {
        try
        {
            var value = _resourceManager.GetString(key, _currentCulture);
            return value ?? key; // 如果找不到資源，返回鍵值本身
        }
        catch
        {
            return key; // 發生錯誤時返回鍵值
        }
    }

    /// <summary>
    /// 套用文化資訊到應用程式
    /// </summary>
    /// <param name="culture">要套用的文化資訊</param>
    private void ApplyCulture(CultureInfo culture)
    {
        // 設定當前執行緒的文化資訊
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        // 設定 WPF 應用程式的文化資訊
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // 更新 WPF 資源字典（如果需要的話）
        UpdateResourceDictionary(culture);
    }

    /// <summary>
    /// 更新 WPF 資源字典
    /// </summary>
    /// <param name="culture">文化資訊</param>
    private void UpdateResourceDictionary(CultureInfo culture)
    {
        if (Application.Current == null) return;

        // 這裡可以根據需要載入不同的資源字典
        // 目前使用 .resx 檔案，所以不需要額外處理
    }
}
