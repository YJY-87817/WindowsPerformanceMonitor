using System.Reflection;

namespace PerformanceMonitor.Reporting.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Reporting_ReferencesOnlyCoreAmongProjectAssemblies()
    {
        string[] projectReferences = Assembly.Load("PerformanceMonitor.Reporting").GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => name.StartsWith("PerformanceMonitor.", StringComparison.Ordinal))
            .ToArray();

        Assert.All(projectReferences, name => Assert.Equal("PerformanceMonitor.Core", name));
    }
}
