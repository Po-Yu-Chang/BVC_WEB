using System.Windows;
using MesMiddleware.Simulator.ViewModels;

namespace MesMiddleware.Simulator;

/// <summary>
/// MES Cloud Simulator 主視窗
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}