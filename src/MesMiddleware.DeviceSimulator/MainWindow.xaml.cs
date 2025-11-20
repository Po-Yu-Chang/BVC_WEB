using MesMiddleware.DeviceSimulator.ViewModels;
using System.Windows;

namespace MesMiddleware.DeviceSimulator;

/// <summary>
/// MainWindow.xaml 的互動邏輯
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
