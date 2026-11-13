using PerformanceMonitor.Presentation.ViewModels;

namespace PerformanceMonitor.Presentation.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Presentation_DoesNotReferenceInfrastructureOrWpf()
    {
        string[] references = [.. typeof(MainViewModel).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty)];

        Assert.Empty(references.Intersect(
            ["PerformanceMonitor.Infrastructure", "PerformanceMonitor.App", "PresentationFramework", "PresentationCore", "WindowsBase"]));
        Assert.Contains("CommunityToolkit.Mvvm", references);
    }
}
