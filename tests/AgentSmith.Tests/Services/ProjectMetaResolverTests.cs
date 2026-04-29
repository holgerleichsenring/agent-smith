using AgentSmith.Infrastructure.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class ProjectMetaResolverTests : IDisposable
{
    private readonly ProjectMetaResolver _sut = new();
    private readonly string _tempDir;

    public ProjectMetaResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "as-meta-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Resolve_RootHasAgentsmithDir_ReturnsRootCandidate()
    {
        var meta = Path.Combine(_tempDir, ".agentsmith");
        Directory.CreateDirectory(meta);

        var result = _sut.Resolve(_tempDir);

        result.Should().Be(meta);
    }

    [Fact]
    public void Resolve_MonorepoSubdir_ReturnsSubdirAgentsmith()
    {
        var subRepo = Path.Combine(_tempDir, "services", "api-gateway");
        var nested = Path.Combine(subRepo, ".agentsmith");
        Directory.CreateDirectory(nested);

        var result = _sut.Resolve(_tempDir);

        result.Should().Be(nested);
    }

    [Fact]
    public void Resolve_NoAgentsmithAnywhere_ReturnsNull()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "src"));

        var result = _sut.Resolve(_tempDir);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_SkipsNodeModulesAndBin()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "node_modules", ".agentsmith"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "bin", ".agentsmith"));
        var real = Path.Combine(_tempDir, "src", ".agentsmith");
        Directory.CreateDirectory(real);

        var result = _sut.Resolve(_tempDir);

        result.Should().Be(real);
    }

    [Fact]
    public void Resolve_NonexistentPath_ReturnsNull()
    {
        var result = _sut.Resolve("/this/does/not/exist/anywhere");

        result.Should().BeNull();
    }
}
