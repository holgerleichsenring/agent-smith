using AgentSmith.Application.Services.BaseRefresh;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-11-24-9dc0a: a conflict pass made progress when fewer paths are open, or as many with fewer marker lines.</summary>
public sealed class BaseMergeProgressTests
{
    private static BaseMergeOpen Open(int markerLines, params string[] paths) =>
        new([.. paths.Select(p => new BaseMergeOpenPath(p, BaseMergeOpenPath.Markers))], markerLines);

    [Fact]
    public void IsProgressOver_FewerPaths_OrFewerMarkerLines_IsProgress()
    {
        Open(9, "a").IsProgressOver(Open(2, "a", "b")).Should().BeTrue();
        Open(2, "a", "b").IsProgressOver(Open(6, "a", "b")).Should().BeTrue();
        Open(6, "a", "b").IsProgressOver(Open(6, "a", "b")).Should().BeFalse();
        Open(2, "a", "b", "c").IsProgressOver(Open(9, "a", "b")).Should().BeFalse();
    }

    [Fact]
    public void Say_NamesEachPathAndWhy() =>
        Open(2, "a.cs").Say().Should().Be("a.cs (conflict markers)");
}
