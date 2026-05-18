using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Triggers;

/// <summary>
/// p0140e: ProjectResolver emits agent_smith_ambiguous_resolution_total once per
/// matched (project, pipeline) pair when more than one project matches the
/// envelope. Zero or single matches do not emit. The counter's tags must include
/// both 'project' and 'pipeline' labels.
/// </summary>
[Collection(MeterCollection.Name)]
public sealed class ProjectResolverAmbiguousMetricsTests
{
    private const string CounterName = "agent_smith_ambiguous_resolution_total";

    private readonly ProjectResolver _sut = new();

    [Fact]
    public void Resolve_SingleMatch_NoAmbiguousIncrement()
    {
        var config = ConfigWith(
            ("alpha", TriggerWithTag("alpha")),
            ("beta", TriggerWithTag("beta")));

        using var capture = MeterCapture.ForCounter(CounterName);
        var matches = _sut.Resolve(config, new IncomingTicketEnvelope { Labels = ["alpha"] });

        matches.Should().ContainSingle();
        capture.Measurements.Should().BeEmpty(
            "ambiguous-resolution counter must not fire when exactly one project matches");
    }

    [Fact]
    public void Resolve_TwoMatches_EmitsTwoIncrementsOnePerMatch()
    {
        var config = ConfigWith(
            ("alpha", TriggerWithTag("shared")),
            ("beta", TriggerWithTag("shared")));

        using var capture = MeterCapture.ForCounter(CounterName);
        var matches = _sut.Resolve(config, new IncomingTicketEnvelope { Labels = ["shared"] });

        matches.Should().HaveCount(2);

        var measurements = capture.Measurements;
        measurements.Should().HaveCount(2);
        measurements.Should().OnlyContain(m => m.Value == 1);

        var labelPairs = measurements
            .Select(m => (
                Project: TagValue(m.Tags, "project"),
                Pipeline: TagValue(m.Tags, "pipeline")))
            .ToList();

        labelPairs.Should().BeEquivalentTo(new[]
        {
            (Project: "alpha", Pipeline: "fix-bug"),
            (Project: "beta",  Pipeline: "fix-bug"),
        });
    }

    [Fact]
    public void Resolve_ThreeMatches_EmitsThreeIncrementsWithCorrectLabels()
    {
        var config = ConfigWith(
            ("alpha", TriggerWithTag("shared")),
            ("beta",  TriggerWithTag("shared")),
            ("gamma", TriggerWithTag("shared")));

        using var capture = MeterCapture.ForCounter(CounterName);
        var matches = _sut.Resolve(config, new IncomingTicketEnvelope { Labels = ["shared"] });

        matches.Should().HaveCount(3);

        var measurements = capture.Measurements;
        measurements.Should().HaveCount(3);

        var labelPairs = measurements
            .Select(m => (
                Project: TagValue(m.Tags, "project"),
                Pipeline: TagValue(m.Tags, "pipeline")))
            .ToList();

        labelPairs.Should().BeEquivalentTo(new[]
        {
            (Project: "alpha", Pipeline: "fix-bug"),
            (Project: "beta",  Pipeline: "fix-bug"),
            (Project: "gamma", Pipeline: "fix-bug"),
        });
        labelPairs.Distinct().Should().HaveCount(3,
            "each matched (project, pipeline) must appear exactly once");
    }

    private static string? TagValue(KeyValuePair<string, object?>[] tags, string key) =>
        tags.FirstOrDefault(t => t.Key == key).Value as string;

    private static AgentSmithConfig ConfigWith(
        params (string Name, WebhookTriggerConfig Trigger)[] entries)
    {
        var projects = new Dictionary<string, ResolvedProject>();
        foreach (var (name, trigger) in entries)
        {
            projects[name] = new ResolvedProject
            {
                Name = name,
                Tracker = new TrackerConnection { Name = $"gh-{name}", Type = TrackerType.GitHub },
                DefaultPipeline = "fix-bug",
                GithubTrigger = trigger,
            };
        }
        return new AgentSmithConfig { Projects = projects };
    }

    private static WebhookTriggerConfig TriggerWithTag(string tag) =>
        new()
        {
            ProjectResolution = new ProjectResolutionConfig
            {
                Strategy = ResolutionStrategy.Tag,
                Value = tag,
            },
            DefaultPipeline = "fix-bug",
        };
}
