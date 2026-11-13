using System.ComponentModel;
using System.Windows;
using PerformanceMonitor.Application.Monitoring;
using PerformanceMonitor.Presentation.ViewModels;

namespace PerformanceMonitor.App.Views;

/// <summary>
/// 主視窗。只負責呈現，資料來自 <see cref="MainViewModel"/>。
/// 關閉視窗（Phase 3 確認：關閉即結束）：先停止監控（補寫最後一筆、寫停止 Log）再結束程式。
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly MonitoringController _controller;
    private bool _stopped;

    /// <summary>建立主視窗並綁定 ViewModel。</summary>
    public MainWindow(MainViewModel viewModel, MonitoringController controller)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _controller = controller;
        DataContext = viewModel;
    }

    /// <inheritdoc />
    protected override async void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnClosing(e);
        if (_stopped || e.Cancel)
        {
            return;
        }

        // 先取消這次關閉，等待監控停止後再關閉一次。
        e.Cancel = true;
        IsEnabled = false;
        Task stopping = _controller.StopAsync();
        _viewModel.Refresh();
        await stopping.ConfigureAwait(true);
        _stopped = true;
        Close();
    }
}
