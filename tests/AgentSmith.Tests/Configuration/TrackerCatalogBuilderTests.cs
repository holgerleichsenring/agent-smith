using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// p0140c: TrackerCatalogBuilder converts RawTrackerEntry (YAML shape) into the typed
/// TrackerConnection record. Verifies the new Polling and ZeroMatchComment fields wire
/// through correctly, including the no-polling-block default.
/// </summary>
public sealed class TrackerCatalogBuilderTests
{
    private readonly TrackerCatalogBuilder _sut = new();

    [Fact]
    public void Build_TrackerWithPollingBlock_BindsPolling()
    {
        var raw = new Dictionary<string, RawTrackerEntry>
        {
            ["tr1"] = new RawTrackerEntry
            {
                Type = TrackerType.GitHub,
                Auth = "token",
                Polling = new RawPollingEntry
                {
                    Enabled = true,
                    IntervalSeconds = 120,
                    JitterPercent = 25,
                },
            }
        };

        var built = _sut.Build(raw, []);
        var tracker = built.Should().ContainKey("tr1").WhoseValue;
        tracker.Polling.Enabled.Should().BeTrue();
        tracker.Polling.IntervalSeconds.Should().Be(120);
        tracker.Polling.JitterPercent.Should().Be(25);
    }

    [Fact]
    public void Build_TrackerWithoutPollingBlock_DefaultsToDisabled()
    {
        var raw = new Dictionary<string, RawTrackerEntry>
        {
            ["tr1"] = new RawTrackerEntry { Type = TrackerType.GitHub, Auth = "token", Polling = null }
        };

        var built = _sut.Build(raw, []);
        built["tr1"].Polling.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Build_TrackerWithZeroMatchComment_BindsField()
    {
        var raw = new Dictionary<string, RawTrackerEntry>
        {
            ["tr1"] = new RawTrackerEntry
            {
                Type = TrackerType.GitHub,
                Auth = "token",
                ZeroMatchComment = true,
            }
        };

        var built = _sut.Build(raw, []);
        built["tr1"].ZeroMatchComment.Should().BeTrue();
    }

    /// <summary>2026-09-17-042ea: the Jira link type a filed child is linked to its parent with.</summary>
    [Fact]
    public void Build_TrackerWithParentLinkType_BindsField()
    {
        var raw = new Dictionary<string, RawTrackerEntry>
        {
            ["tr1"] = new RawTrackerEntry { Type = TrackerType.Jira, Auth = "token", ParentLinkType = "Parent of" },
        };

        _sut.Build(raw, [])["tr1"].ParentLinkType.Should().Be("Parent of");
    }
}
