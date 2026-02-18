using AgentSmith.Infrastructure.Providers.Agent;
using FluentAssertions;

namespace AgentSmith.Tests.Providers;

public class FileReadTrackerTests
{
    private readonly FileReadTracker _sut = new();

    [Fact]
    public void HasBeenRead_ReturnsFalse_WhenNotRead()
    {
        _sut.HasBeenRead("src/Program.cs").Should().BeFalse();
    }

    [Fact]
    public void HasBeenRead_ReturnsTrue_AfterTrackRead()
    {
        _sut.TrackRead("src/Program.cs");

        _sut.HasBeenRead("src/Program.cs").Should().BeTrue();
    }

    [Fact]
    public void TrackRead_IncrementsCount()
    {
        _sut.TrackRead("file.cs");
        _sut.TrackRead("file.cs");
        _sut.TrackRead("file.cs");

        _sut.GetReadCount("file.cs").Should().Be(3);
    }

    [Fact]
    public void GetReadCount_ReturnsZero_WhenNotRead()
    {
        _sut.GetReadCount("unknown.cs").Should().Be(0);
    }

    [Fact]
    public void InvalidateRead_ResetsState()
    {
        _sut.TrackRead("file.cs");
        _sut.HasBeenRead("file.cs").Should().BeTrue();

        _sut.InvalidateRead("file.cs");

        _sut.HasBeenRead("file.cs").Should().BeFalse();
        _sut.GetReadCount("file.cs").Should().Be(0);
    }

    [Fact]
    public void GetAllReadFiles_ReturnsTrackedPaths()
    {
        _sut.TrackRead("a.cs");
        _sut.TrackRead("b.cs");
        _sut.TrackRead("c.cs");

        _sut.GetAllReadFiles().Should().BeEquivalentTo(["a.cs", "b.cs", "c.cs"]);
    }

    [Fact]
    public void CaseInsensitive_PathComparison()
    {
        _sut.TrackRead("Src/Program.CS");

        _sut.HasBeenRead("src/program.cs").Should().BeTrue();
        _sut.GetReadCount("SRC/PROGRAM.cs").Should().Be(1);
    }
}
