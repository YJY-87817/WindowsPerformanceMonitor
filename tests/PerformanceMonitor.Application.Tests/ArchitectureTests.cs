using PerformanceMonitor.Application.Services;

namespace PerformanceMonitor.Application.Tests;

public class ArchitectureTests
{
    private static readonly string[] Forbidden =
    [
        "PerformanceMonitor.Infrastructure",
        "PerformanceMonitor.App",
        "PresentationFramework",
        "PresentationCore",
        "WindowsBase",
    ];

    [Fact]
    public void Application_DoesNotReferenceInfrastructureOrWpf()
    {
        string[] references = typeof(SelfCheckService).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        Assert.Empty(references.Intersect(Forbidden));
    }
}
