using System.Windows;
using System.Windows.Controls;

namespace MesMiddleware.Monitor.Views;

/// <summary>
/// TraceVerificationView.xaml 的互動邏輯
/// </summary>
public partial class TraceVerificationView : UserControl
{
    public TraceVerificationView()
    {
        InitializeComponent();

        // 設定初始焦點到工單號輸入框
        Loaded += (s, e) => WorkOrderTextBox.Focus();
    }
}
