using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Core.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Core_ReferencesOnlyTheBaseClassLibrary()
    {
        string[] references = typeof(MonitorStatus).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        Assert.All(references, name => Assert.True(
            name.StartsWith("System", StringComparison.Ordinal) || name is "netstandard" or "mscorlib",
            $"Core 不得參考 {name}"));
    }
}
