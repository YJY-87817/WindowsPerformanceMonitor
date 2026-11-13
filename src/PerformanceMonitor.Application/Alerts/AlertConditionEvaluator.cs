using System.Globalization;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Alerts;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Application.Alerts;

/// <summary>某個告警對象本次的判斷結果與顯示文字。</summary>
/// <param name="Target">對象。</param>
/// <param name="Condition">判斷。</param>
/// <param name="ValueText">目前數值。</param>
/// <param name="Threshold">門檻說明。</param>
public sealed record AlertCheck(string Target, AlertCondition Condition, string ValueText, string Threshold);

/// <summary>
/// 由快照判斷每個告警對象（CPU、RAM、各固定磁碟）的狀況（舊規格 v1.5 §6.1、§6.3、§6.4）。
/// 只有狀態為 OK 且有數值時才判斷；否則為 <see cref="AlertCondition.Unknown"/>。
/// </summary>
public static class AlertConditionEvaluator
{
    /// <summary>判斷所有對象。快照中沒有的模組或磁碟不會出現在結果中。</summary>
    public static IReadOnlyList<AlertCheck> Evaluate(MonitorSnapshot snapshot, ThresholdSettings thresholds)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(thresholds);
        var checks = new List<AlertCheck>();

        if (snapshot.Find(ModuleNames.Cpu) is ModuleResult cpu)
        {
            checks.Add(Usage("CPU", cpu.Status, (cpu as CpuResult)?.UsagePercent, thresholds.CpuPercent, thresholds.CpuRecoveryPercent));
        }

        if (snapshot.Find(ModuleNames.Memory) is ModuleResult memory)
        {
            checks.Add(Usage("RAM", memory.Status, (memory as MemoryResult)?.UsagePercent, thresholds.RamPercent, thresholds.RamRecoveryPercent));
        }

        if (snapshot.Find(ModuleNames.Disk) is DiskResult disks)
        {
            string threshold = DiskThresholdText(thresholds);
            foreach (DiskItem disk in disks.Disks)
            {
                checks.Add(new AlertCheck(
                    $"Disk {disk.Drive}",
                    DiskCondition(disk, thresholds),
                    Invariant($"{Format(disk.FreePercent, "0.0")}% / {Format(disk.FreeGB, "0.00")} GB free"),
                    threshold));
            }
        }

        return checks;
    }

    /// <summary>
    /// 磁碟條件：any 為任一已設定條件達低空間門檻即觸發、所有條件達恢復值才恢復；
    /// all 為所有條件都達門檻才觸發、任一條件達恢復值即恢復。未設定 GB 門檻時只看百分比。
    /// </summary>
    public static AlertCondition DiskCondition(DiskItem disk, ThresholdSettings thresholds)
    {
        ArgumentNullException.ThrowIfNull(disk);
        ArgumentNullException.ThrowIfNull(thresholds);
        if (disk.Status != MonitorStatus.OK || disk.FreePercent is not double freePercent || disk.FreeGB is not double freeGB)
        {
            return AlertCondition.Unknown;
        }

        var low = new List<bool> { freePercent <= thresholds.DiskFreePercent };
        var recovered = new List<bool> { freePercent >= thresholds.DiskRecoveryFreePercent };
        if (thresholds.DiskFreeGB is double triggerGB && thresholds.DiskRecoveryFreeGB is double recoveryGB)
        {
            low.Add(freeGB <= triggerGB);
            recovered.Add(freeGB >= recoveryGB);
        }

        bool all = thresholds.DiskConditionMode == DiskConditionMode.All;
        bool breach = all ? low.All(x => x) : low.Any(x => x);
        bool isRecovered = all ? recovered.Any(x => x) : recovered.All(x => x);
        return breach ? AlertCondition.Breach : isRecovered ? AlertCondition.Recovered : AlertCondition.Hysteresis;
    }

    private static AlertCheck Usage(string target, MonitorStatus status, double? value, double trigger, double recovery)
    {
        AlertCondition condition = AlertCondition.Unknown;
        if (status == MonitorStatus.OK && value is double v)
        {
            condition = v >= trigger ? AlertCondition.Breach : v <= recovery ? AlertCondition.Recovered : AlertCondition.Hysteresis;
        }

        return new AlertCheck(
            target,
            condition,
            value is null ? "N/A" : $"{Format(value, "0.0")}%",
            Invariant($">= {trigger}% (recover <= {recovery}%)"));
    }

    private static string DiskThresholdText(ThresholdSettings t)
    {
        string text = Invariant($"free <= {t.DiskFreePercent}%");
        if (t.DiskFreeGB is double gb)
        {
            string joiner = t.DiskConditionMode == DiskConditionMode.All ? "and" : "or";
            text += Invariant($" {joiner} <= {gb} GB");
        }

        return text;
    }

    private static string Format(double? value, string format) =>
        value?.ToString(format, CultureInfo.InvariantCulture) ?? "N/A";

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
