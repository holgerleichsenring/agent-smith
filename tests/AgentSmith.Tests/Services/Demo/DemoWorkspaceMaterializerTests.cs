using AgentSmith.Application.Services.Demo;
using AgentSmith.Infrastructure.Core.Services.Demo;
using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Demo;

/// <summary>
/// p0326: runs the REAL chain — embedded tarball resource → extractor → local
/// git init — so the test proves the exact workspace `agent-smith demo` hands
/// to the pipeline: a git repo with the seeded boundary bug and the pinned
/// failing test, plus the .agentsmith meta the BootstrapGate requires.
/// </summary>
public sealed class DemoWorkspaceMaterializerTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), $"agentsmith-demo-test-{Guid.NewGuid():N}");

    [Fact]
    public async Task DemoWorkspaceMaterializer_FreshDir_CreatesGitRepoWithSeededBug()
    {
        var materializer = new DemoWorkspaceMaterializer(
            new EmbeddedDemoSample(),
            new CatalogTarballExtractor(NullLogger<CatalogTarballExtractor>.Instance),
            new LocalGitProcessInitializer(),
            NullLogger<DemoWorkspaceMaterializer>.Instance);

        var workspace = await materializer.MaterializeAsync(_dir, CancellationToken.None);

        Directory.Exists(Path.Combine(workspace, ".git"))
            .Should().BeTrue("the workspace must be a local git repo with a baseline commit");
        var calculator = await File.ReadAllTextAsync(
            Path.Combine(workspace, "src", "Sample", "PriceCalculator.cs"));
        calculator.Should().Contain("orderAmount > BulkThreshold",
            "the seeded bug (strict > at the boundary) must ship as-is");
        File.Exists(Path.Combine(workspace, "tests", "Sample.Tests", "PriceCalculatorTests.cs"))
            .Should().BeTrue("the pinned failing test is the bug's specification");
        File.Exists(Path.Combine(workspace, ".agentsmith", "contexts", "default", "context.yaml"))
            .Should().BeTrue("BootstrapGate requires the per-context meta");
        File.Exists(Path.Combine(workspace, ".agentsmith", "contexts", "default", "coding-principles.md"))
            .Should().BeTrue("BootstrapGate requires the per-context meta");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}
