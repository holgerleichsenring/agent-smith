using AgentSmith.Application.Services.Metrics;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using FluentAssertions;

namespace AgentSmith.Tests.Triggers;

/// <summary>
/// p0315d: trigger routing by ticket kind. A ticket the framework FILED routes hard-bound to the
/// phase-execution preset in ProjectResolver — before pipeline_from_label, which would otherwise
/// drop it. Every other ticket keeps today's label routing (bug -> fix-bug).
/// <para>
/// 2026-09-22-766b: the bind reads the APPROVAL, not a word. The word it used to read was written
/// by this framework and read by this framework and nobody else, while the approval stamp on the
/// same ticket says the same thing — so one key asserts the fact now.
/// </para>
/// <para>
/// Three of these tests are the reason the obvious move (stop writing the word, read nothing)
/// was refused. A project resolved by REPOSITORY or by AREA PATH cannot be matched from a polled
/// envelope, which carries labels, ticket id and platform only — but the tracker's own webhook
/// carries both, so those projects DO match there, and the hard bind is then the only thing that
/// chooses a pipeline for them: no operator's label map holds a key a filed ticket carries.
/// Removing the bind without replacing it would leave the match standing and the pipeline gone —
/// a run that never starts, with nothing reporting it.
/// </para>
/// </summary>
public sealed class PhaseExecutionRoutingTests
{
    private const string RepoUrl = "https://git.test/sample/app";

    private readonly ProjectResolver _sut = new(new AgentSmithMetrics(), new PipelineResolver());

    [Fact]
    public void Routing_AFiledTicketCarryingTheApprovalStamp_BindsToPhaseExecution()
    {
        var matches = _sut.Resolve(
            TagResolved(), Envelope("proj", FiledTicketLabels.ApprovedSetStamp));

        matches.Should().ContainSingle(
            m => m.PipelineName == PipelinePresets.PhaseExecutionName,
            "a ticket filed from an approved set must route to the phase-execution preset even "
            + "though no pipeline_from_label entry maps the framework-owned stamp");
    }

    [Fact]
    public void Routing_ATicketCarryingOnlyThePhaseWord_IsRoutedByTheOperatorsOwnRules()
    {
        var matches = _sut.Resolve(TagResolved(), Envelope("proj", "phase", "bug"));

        matches.Should().ContainSingle(
            m => m.PipelineName == "fix-bug",
            "the word is nobody's outside this framework and binds nothing any more — this "
            + "project's own label map decided, exactly as it does for every other ticket");
    }

    [Fact]
    public void Routing_ARepoResolvedProjectOnTheWebhook_StillRoutesAFiledTicket()
    {
        var envelope = EnvelopeFrom(RepoUrl, null, FiledTicketLabels.ApprovedSetStamp);

        var matches = _sut.Resolve(RepoResolved(), envelope);

        matches.Should().ContainSingle(
            m => m.PipelineName == PipelinePresets.PhaseExecutionName,
            "the webhook carries the source repository, so this project matches there — and the "
            + "bind is the only thing that can name a pipeline for it");
    }

    [Fact]
    public void Routing_AnAreaPathResolvedProjectOnTheWebhook_StillRoutesAFiledTicket()
    {
        var envelope = EnvelopeFrom(
            null, @"Contoso\Platform\Widgets", FiledTicketLabels.ApprovedSetStamp);

        var matches = _sut.Resolve(AreaPathResolved(), envelope);

        matches.Should().ContainSingle(
            m => m.PipelineName == PipelinePresets.PhaseExecutionName,
            "a work-item webhook carries the area path, so this project matches there too");
    }

    [Fact]
    public void Routing_ATagResolvedProjectOnThePoll_StillRoutesAFiledTicket()
    {
        // A POLLED envelope: labels, ticket id and platform, and nothing else.
        var matches = _sut.Resolve(
            TagResolved(), Envelope("proj", FiledTicketLabels.ApprovedSetStamp));

        matches.Should().ContainSingle()
            .Which.PipelineName.Should().Be(PipelinePresets.PhaseExecutionName);
    }

    [Fact]
    public void Routing_ARecordLabelledTicket_IsStillRefusedBeforeEveryOtherRule()
    {
        var matches = _sut.Resolve(
            TagResolved(),
            Envelope("proj", PhaseTicketRenderer.EpicLabel, FiledTicketLabels.ApprovedSetStamp));

        matches.Should().BeEmpty(
            "the record refusal comes FIRST — before the bind, whatever else the ticket carries — "
            + "because a record is not work and every other rule ends in something");
    }

    [Fact]
    public void Routing_BugTicket_StillSelectsFixBug()
    {
        var matches = _sut.Resolve(TagResolved(), Envelope("proj", "bug"));

        matches.Should().ContainSingle(
            m => m.PipelineName == "fix-bug",
            "a bug ticket keeps today's pipeline_from_label routing untouched");
    }

    private static IncomingTicketEnvelope Envelope(params string[] labels) =>
        EnvelopeFrom(null, null, labels);

    private static IncomingTicketEnvelope EnvelopeFrom(
        string? sourceRepoUrl, string? areaPath, params string[] labels) => new()
    {
        TicketId = "1",
        Platform = "github",
        Labels = labels,
        SourceRepoUrl = sourceRepoUrl,
        AreaPath = areaPath,
    };

    // One project with the typical bug -> fix-bug label map. The map is a strict filter: without
    // the hard bind a filed ticket would be dropped here, never routed.
    private static AgentSmithConfig TagResolved() =>
        Config(new ProjectResolutionConfig { Strategy = ResolutionStrategy.Tag, Value = "proj" });

    private static AgentSmithConfig RepoResolved() =>
        Config(new ProjectResolutionConfig { Strategy = ResolutionStrategy.Repo, Value = RepoUrl },
            repos: [new RepoConnection { Name = "app", Url = RepoUrl }]);

    private static AgentSmithConfig AreaPathResolved() =>
        Config(new ProjectResolutionConfig
        {
            Strategy = ResolutionStrategy.AreaPath,
            Value = @"Contoso\Platform",
        });

    private static AgentSmithConfig Config(
        ProjectResolutionConfig resolution, IReadOnlyList<RepoConnection>? repos = null) => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["alpha"] = new ResolvedProject
            {
                Name = "alpha",
                Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
                DefaultPipeline = "fix-bug",
                Repos = repos ?? [],
                GithubTrigger = new WebhookTriggerConfig
                {
                    ProjectResolution = resolution,
                    DefaultPipeline = "fix-bug",
                    PipelineFromLabel = new Dictionary<string, string> { ["bug"] = "fix-bug" },
                },
            },
        },
        PipelineTriggers = PipelineTriggerMap.Empty,
    };
}
