using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PerformanceMonitor.App.Views;
using PerformanceMonitor.Application;
using PerformanceMonitor.Application.Monitoring;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure;
using PerformanceMonitor.Infrastructure.Startup;
using PerformanceMonitor.Infrastructure.Storage;
using PerformanceMonitor.Presentation.ViewModels;

namespace PerformanceMonitor.App;

/// <summary>
/// WPF 程式進入點，也是 DI 組裝點（Composition Root）。
/// 啟動：讀取設定 → 檢查資料目錄 → 沒有錯誤時寫啟動 Log 並自動開始監控（Phase 3 確認：開啟即監控）；
/// 有錯誤時仍開啟視窗並顯示原因，但不開始監控。畫面依 <c>monitor.refreshSeconds</c> 更新。
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;
    private DispatcherTimer? _refreshTimer;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDataPaths paths = AppDataPaths.CreateDefault();
        StartupCheck check = MonitoringStartup.Check(paths);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(e.Args);
        builder.Logging.ClearProviders(); // 程式事件寫在專用 Log 檔
        builder.Services
            .AddPerformanceMonitorApplication()
            .AddPerformanceMonitorInfrastructure();
        builder.Services.AddSingleton(check.EffectiveSettings);
        builder.Services.AddSingleton(paths);
        builder.Services.AddSingleton(new StartupStatus(check.Errors, check.Settings.Warnings));
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();
        _host = builder.Build();

        if (check.CanMonitor)
        {
            MonitoringStartup.WriteStartupLog(_host.Services, GetVersion(), check, paths);
            _host.Services.GetRequiredService<MonitoringController>().Start();
        }

        MainViewModel viewModel = _host.Services.GetRequiredService<MainViewModel>();
        MainWindow window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_host.Services.GetRequiredService<AppSettings>().Monitor.RefreshSeconds),
        };
        _refreshTimer.Tick += (_, _) => viewModel.Refresh();
        _refreshTimer.Start();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _refreshTimer?.Stop();
        _host?.Dispose();
        base.OnExit(e);
    }

    private static string GetVersion() =>
        typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
}
