using System.Windows;
using PerformanceMonitor.App.ViewModels;

namespace PerformanceMonitor.App.Views;

/// <summary>
/// 主視窗。只負責呈現，資料來自 <see cref="MainViewModel"/>。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>建立主視窗並綁定 ViewModel。</summary>
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
