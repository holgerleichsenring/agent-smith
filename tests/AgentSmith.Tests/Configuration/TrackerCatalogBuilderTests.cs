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
        var errors = new List<string>();

        var built = _sut.Build(raw, errors);

        errors.Should().BeEmpty();
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
        var errors = new List<string>();

        var built = _sut.Build(raw, errors);

        errors.Should().BeEmpty();
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
        var errors = new List<string>();

        var built = _sut.Build(raw, errors);

        errors.Should().BeEmpty();
        built["tr1"].ZeroMatchComment.Should().BeTrue();
    }
}
