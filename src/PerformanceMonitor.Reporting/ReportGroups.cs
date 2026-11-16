namespace PerformanceMonitor.Reporting;

/// <summary>報告的指標分組與模組名稱（顯示順序即宣告順序）。ACPI 熱區不得稱為 CPU 溫度。</summary>
public static class ReportGroups
{
    /// <summary>CPU。</summary>
    public const string Cpu = "CPU";

    /// <summary>記憶體。</summary>
    public const string Memory = "記憶體";

    /// <summary>磁碟容量。</summary>
    public const string Disk = "磁碟容量";

    /// <summary>磁碟 I/O。</summary>
    public const string DiskIo = "磁碟 I/O";

    /// <summary>NVIDIA GPU。</summary>
    public const string Gpu = "GPU";

    /// <summary>網路。</summary>
    public const string Network = "網路";

    /// <summary>程序。</summary>
    public const string Process = "程序";

    /// <summary>GPU 溫度。</summary>
    public const string GpuTemperature = "GPU 溫度";

    /// <summary>ACPI 熱區（不是 CPU 核心溫度）。</summary>
    public const string ThermalZone = "ACPI 熱區";

    /// <summary>指標分組的顯示順序。</summary>
    public static IReadOnlyList<string> Order { get; } = [Cpu, Memory, Disk, DiskIo, Gpu, Network, GpuTemperature, ThermalZone];

    /// <summary>排序用的位置；未知分組排最後。</summary>
    public static int IndexOf(string group)
    {
        int index = Order.ToList().IndexOf(group);
        return index < 0 ? int.MaxValue : index;
    }
}
