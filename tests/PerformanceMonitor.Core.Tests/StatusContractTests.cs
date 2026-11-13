using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Core.Tests;

/// <summary>
/// 狀態與錯誤分類是資料契約（寫入 CSV），名稱不得無聲變更。
/// </summary>
public class StatusContractTests
{
    [Fact]
    public void MonitorStatus_HasExactlyTheFiveSpecifiedNames()
    {
        Assert.Equal(
            ["OK", "WarmingUp", "Unavailable", "Error", "Disabled"],
            Enum.GetNames<MonitorStatus>());
    }

    [Fact]
    public void ErrorCode_HasExactlyTheEightSpecifiedNames()
    {
        Assert.Equal(
            ["Unavailable", "ConfigurationError", "Timeout", "DataError", "AccessDenied", "IOError", "TransientError", "InternalError"],
            Enum.GetNames<ErrorCode>());
    }
}
