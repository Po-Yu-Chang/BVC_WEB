using System.Globalization;

namespace MesMiddleware.Monitor.Services;

/// <summary>
/// 本地化服務介面
/// 提供多語言切換和資源字串存取功能
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// 當前文化資訊
    /// </summary>
    CultureInfo CurrentCulture { get; }

    /// <summary>
    /// 語言變更事件
    /// </summary>
    event EventHandler? LanguageChanged;

    /// <summary>
    /// 變更語言
    /// </summary>
    /// <param name="cultureName">文化名稱（例如：en, zh-CN, zh-TW）</param>
    void ChangeLanguage(string cultureName);

    /// <summary>
    /// 取得本地化字串
    /// </summary>
    /// <param name="key">資源鍵值</param>
    /// <returns>本地化後的字串</returns>
    string GetString(string key);
}
