using System.Globalization;
using System.Windows.Data;
using MesMiddleware.Monitor.Services;

namespace MesMiddleware.Monitor.Converters;

/// <summary>
/// 本地化字串轉換器
/// 將資源鍵值轉換為本地化後的字串
/// </summary>
public class LocalizationConverter : IValueConverter
{
    private static ILocalizationService? _localizationService;

    /// <summary>
    /// 設定本地化服務
    /// </summary>
    public static void SetLocalizationService(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    /// <summary>
    /// 將資源鍵轉換為本地化字串
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is string key && _localizationService != null)
        {
            return _localizationService.GetString(key);
        }
        return parameter?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 不支援反向轉換
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
