namespace PerformanceMonitor.Core.Models.Results;

/// <summary>各監控模組的名稱（<see cref="ModuleResult.Module"/>），也用於 Log 的來源欄位。</summary>
public static class ModuleNames
{
    /// <summary>CPU。</summary>
    public const string Cpu = "Cpu";

    /// <summary>記憶體。</summary>
    public const string Memory = "Memory";

    /// <summary>磁碟容量。</summary>
    public const string Disk = "Disk";

    /// <summary>磁碟 I/O。</summary>
    public const string DiskIo = "DiskIo";

    /// <summary>NVIDIA GPU。</summary>
    public const string Gpu = "Gpu";

    /// <summary>網路。</summary>
    public const string Network = "Network";

    /// <summary>程序。</summary>
    public const string Process = "Process";

    /// <summary>ACPI 熱區溫度。</summary>
    public const string Temperature = "Temperature";
}
