using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Presentation.Text;

namespace PerformanceMonitor.Presentation.Tests;

public class ModuleSummaryTests
{
    public static TheoryData<ModuleResult, string> Cases => new()
    {
        { new CpuResult(MonitorStatus.OK, 25.55), "25.6%" },
        { new CpuResult(MonitorStatus.WarmingUp, null), "—" },
        { new MemoryResult(MonitorStatus.OK, 16, 10.4321, 5.5679, 65.2), "65.2%（10.43 / 16.00 GB）" },
        { new MemoryResult(MonitorStatus.Error, null, null, null, null), "—" },
        {
            new DiskResult(MonitorStatus.Error, [
                new DiskItem(MonitorStatus.OK, "C:", null, 100, 72.5, 27.5, 72.5, 27.5),
                new DiskItem(MonitorStatus.Error, "D:", null, null, null, null, null, null),
            ]),
            "C: 72.5%（可用 27.50 GB）；D: 錯誤（Error）"
        },
        {
            new DiskIoResult(MonitorStatus.OK, [
                new DiskIoItem(MonitorStatus.OK, DiskIoScope.Logical, "C:", 1.234, 0.5, 10, 5, 1, 2, 3.04),
                new DiskIoItem(MonitorStatus.OK, DiskIoScope.Physical, "0 C:", 1.234, 0.5, 10, 5, 1, 2, 3.04),
            ]),
            "C: 讀 1.23 / 寫 0.50 MB/s，忙碌 3.0%"
        },
        { new GpuResult(MonitorStatus.OK, [new GpuItem(MonitorStatus.OK, 0, "RTX 4060", 35, 2048, 8192, 61)]), "GPU 0 RTX 4060：35.0%，顯存 2048 / 8192 MB，61°C" },
        { new GpuResult(MonitorStatus.Unavailable, []), "—" },
        {
            new NetworkResult(MonitorStatus.OK, [
                new NetworkAdapterItem(MonitorStatus.OK, "Ethernet", null, null, true, 1, 1, 12.345, 1.5),
                new NetworkAdapterItem(MonitorStatus.Unavailable, "Wi-Fi", null, null, null, null, null, null, null),
            ], 12.345, 1.5),
            "下載 12.35 / 上傳 1.50 Mbps（1 / 2 張介面卡）"
        },
        { new NetworkResult(MonitorStatus.Unavailable, [], null, null), "—" },
        {
            new ProcessResult(MonitorStatus.OK, [new ProcessItem(1, "chrome", 1, 1200.55, null), new ProcessItem(2, "code", 2, 850.2, null), new ProcessItem(3, "a", 3, 3, null), new ProcessItem(4, "b", 4, 1, null)], 0, 0),
            "chrome 1200.6 MB；code 850.2 MB；a 3.0 MB"
        },
        { new TemperatureResult(MonitorStatus.OK, [new ThermalZoneItem(MonitorStatus.OK, "TZ0", 27.95)]), "TZ0 28.0°C" },
        { ModuleResult.Disabled(ModuleNames.Gpu), "—" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void SummarizesWithoutFakeValues(ModuleResult result, string expected) =>
        Assert.Equal(expected, ModuleSummary.For(result));

    [Theory]
    [InlineData(MonitorStatus.OK, "正常（OK）", StatusKind.Ok)]
    [InlineData(MonitorStatus.WarmingUp, "暖機中（WarmingUp）", StatusKind.Pending)]
    [InlineData(MonitorStatus.Unavailable, "無法使用（Unavailable）", StatusKind.Inactive)]
    [InlineData(MonitorStatus.Error, "錯誤（Error）", StatusKind.Error)]
    [InlineData(MonitorStatus.Disabled, "已停用（Disabled）", StatusKind.Inactive)]
    public void StatusTextKeepsEnglishCode(MonitorStatus status, string text, StatusKind kind)
    {
        Assert.Equal(text, UiText.Status(status));
        Assert.Equal(kind, UiText.Kind(status));
    }

    [Fact]
    public void ThermalZonesAreNeverCalledCpuTemperature() =>
        Assert.Equal("ACPI 熱區", UiText.ModuleTitle(ModuleNames.Temperature));
}
