using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace MesMiddleware.DeviceSimulator.ViewModels;

/// <summary>
/// 可編輯的 ParamData 項目 (用於 DataGrid)
/// 對應 LabVIEW 格式: {"code": "xxx", "name": "xxx", "value": "xxx", "unit": "xxx", "desc": "xxx"}
/// </summary>
public partial class EditableParamDataItem : ObservableObject
{
    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private string _desc = string.Empty;

    public EditableParamDataItem() { }

    public EditableParamDataItem(string code, string name, string value, string unit = "", string desc = "")
    {
        Code = code;
        Name = name;
        Value = value;
        Unit = unit;
        Desc = string.IsNullOrEmpty(desc) ? code : desc;
    }
}

/// <summary>
/// 可編輯的 Benchmark 項目 (用於 DataGrid)
/// 對應 LabVIEW 格式: {"code": "xxx", "name": "xxx", "value": "xxx", "unit": "xxx", "desc": "xxx"}
/// </summary>
public partial class EditableBenchmarkItem : ObservableObject
{
    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private string _desc = string.Empty;

    public EditableBenchmarkItem() { }

    public EditableBenchmarkItem(string code, string name, string value, string unit = "", string desc = "")
    {
        Code = code;
        Name = name;
        Value = value;
        Unit = unit;
        Desc = string.IsNullOrEmpty(desc) ? name : desc;
    }
}

/// <summary>
/// 可編輯的 OtherData 項目 (用於 DataGrid)
/// 對應 LabVIEW 格式: {"code": "xxx", "name": "xxx", "value": "xxx", "unit": "xxx", "desc": "xxx"}
/// </summary>
public partial class EditableOtherDataItem : ObservableObject
{
    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private string _desc = string.Empty;

    public EditableOtherDataItem() { }

    public EditableOtherDataItem(string code, string name, string value, string unit = "", string desc = "")
    {
        Code = code;
        Name = name;
        Value = value;
        Unit = unit;
        Desc = string.IsNullOrEmpty(desc) ? code : desc;
    }
}

/// <summary>
/// LabVIEW 格式的預設資料模板
/// 依據 labview.txt 實際發送格式設計
/// </summary>
public static class LabViewDataTemplates
{
    /// <summary>
    /// 生成預設的 ParamData 項目 (MES 要求的標準參數)
    /// </summary>
    public static ObservableCollection<EditableParamDataItem> GetDefaultParamData()
    {
        return new ObservableCollection<EditableParamDataItem>
        {
            new("Result", "Result", "PASS", "", "Result"),
            new("CheckQty", "CheckQty", "2", "pcs", "CheckQty"),
            new("DefectQty", "DefectQty", "0", "pcs", "DefectQty"),
            new("OkQty", "OkQty", "2", "pcs", "OkQty")
        };
    }

    /// <summary>
    /// 生成預設的 Benchmarks 項目 (檢測參數 + 缺陷數量 + 量測值)
    /// </summary>
    public static ObservableCollection<EditableBenchmarkItem> GetDefaultBenchmarks()
    {
        return new ObservableCollection<EditableBenchmarkItem>
        {
            // 檢測參數設定
            new("Check_Param_01", "Upper bore", "Enable", "", "Test"),
            new("Check_Param_02", "Ubore_Diameter_UpperLimit", "525.53", "um", "Test"),
            new("Check_Param_03", "Ubore_Diameter_LowerLimit", "474.73", "um", "Test"),
            new("Check_Param_04", "Lower bore", "Enable", "", "Test"),
            new("Check_Param_05", "Lbore_Diameter_UpperLimit", "500.13", "um", "Test"),
            new("Check_Param_06", "Lbore_Diameter_LowerLimit", "25.4", "um", "Test"),
            new("Check_Param_07", "Shift", "Enable", "", "Test"),
            new("Check_Param_08", "Shift_UpperLimit", "50.8", "um", "Test"),
            new("Check_Param_09", "Shift_LowerLimit", "0", "um", "Test"),
            new("Check_Param_10", "Bore Depth", "Enable", "", "Test"),
            new("Check_Param_11", "Bore_Depth_UpperLimit", "871.73", "um", "Test"),
            new("Check_Param_12", "Bore_Depth_LowerLimit", "770.13", "um", "Test"),

            // 量測值 (第1點)
            new("Ubore_Diameter_X_01", "Ubore_Diameter_X", "507.9248", "um", "Ubore_Diameter_X"),
            new("Ubore_Diameter_Y_01", "Ubore_Diameter_Y", "512.0314", "um", "Ubore_Diameter_Y"),
            new("Ubore_Average_01", "Ubore_Average", "509.9781", "um", "Ubore_Average"),
            new("Lbore_Diameter_X_01", "Lbore_Diameter_X", "133.6302", "um", "Lbore_Diameter_X"),
            new("Lbore_Diameter_Y_01", "Lbore_Diameter_Y", "134.7252", "um", "Lbore_Diameter_Y"),
            new("Lbore_Average_01", "Lbore_Average", "134.1777", "um", "Lbore_Average"),
            new("Shift_X_01", "Shift_X", "29.3906", "um", "Shift_X"),
            new("Shift_Y_01", "Shift_Y", "29.3906", "um", "Shift_Y"),
            new("Shift_Average_01", "Shift_Average", "29.3906", "um", "Shift_Average"),
            new("Bore_Depth_01", "Bore_Depth", "820.5", "um", "Bore_Depth"),

            // 量測值 (第2點)
            new("Ubore_Diameter_X_02", "Ubore_Diameter_X", "506.5812", "um", "Ubore_Diameter_X"),
            new("Ubore_Diameter_Y_02", "Ubore_Diameter_Y", "509.3189", "um", "Ubore_Diameter_Y"),
            new("Ubore_Average_02", "Ubore_Average", "507.9501", "um", "Ubore_Average"),
            new("Lbore_Diameter_X_02", "Lbore_Diameter_X", "232.2753", "um", "Lbore_Diameter_X"),
            new("Lbore_Diameter_Y_02", "Lbore_Diameter_Y", "233.3704", "um", "Lbore_Diameter_Y"),
            new("Lbore_Average_02", "Lbore_Average", "232.8229", "um", "Lbore_Average"),
            new("Shift_X_02", "Shift_X", "12.6202", "um", "Shift_X"),
            new("Shift_Y_02", "Shift_Y", "12.6202", "um", "Shift_Y"),
            new("Shift_Average_02", "Shift_Average", "12.6202", "um", "Shift_Average"),
            new("Bore_Depth_02", "Bore_Depth", "815.3", "um", "Bore_Depth")
        };
    }

    /// <summary>
    /// 生成預設的 OtherData 項目
    /// </summary>
    public static ObservableCollection<EditableOtherDataItem> GetDefaultOtherData()
    {
        return new ObservableCollection<EditableOtherDataItem>
        {
            new("CheckTime", "CheckTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), "", "CheckTime")
        };
    }
}
