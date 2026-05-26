using AgentSmith.Server.Api;
using FluentAssertions;

namespace AgentSmith.Tests.Api;

public sealed class PathTraversalGuardTests : IDisposable
{
    private readonly string _root;

    public PathTraversalGuardTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "guard-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "result.md"), "ok");
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "sub", "nested.json"), "{}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void TryResolveWithin_LegitFile_ReturnsTrue()
    {
        PathTraversalGuard.TryResolveWithin(_root, "result.md", out var resolved).Should().BeTrue();
        resolved.Should().EndWith("result.md");
    }

    [Fact]
    public void TryResolveWithin_NestedFile_ReturnsTrue()
    {
        PathTraversalGuard.TryResolveWithin(_root, "sub/nested.json", out var resolved).Should().BeTrue();
        resolved.Should().EndWith("nested.json");
    }

    [Fact]
    public void TryResolveWithin_RelativeEscape_ReturnsFalse()
    {
        PathTraversalGuard.TryResolveWithin(_root, "../etc/passwd", out _).Should().BeFalse();
        PathTraversalGuard.TryResolveWithin(_root, "../../etc/passwd", out _).Should().BeFalse();
        PathTraversalGuard.TryResolveWithin(_root, "sub/../../etc/passwd", out _).Should().BeFalse();
    }

    [Fact]
    public void TryResolveWithin_AbsolutePath_ReturnsFalse()
    {
        PathTraversalGuard.TryResolveWithin(_root, "/etc/passwd", out _).Should().BeFalse();
    }

    [Fact]
    public void TryResolveWithin_NonExistentFile_ReturnsFalse()
    {
        PathTraversalGuard.TryResolveWithin(_root, "does-not-exist.md", out _).Should().BeFalse();
    }
}
