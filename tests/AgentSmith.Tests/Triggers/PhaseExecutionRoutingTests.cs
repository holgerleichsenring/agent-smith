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
/// 2026-09-22-766b: TWO KEYS BIND, FOR TWO DIFFERENT REASONS. A FILING writes the approval stamp
/// and nothing else — "somebody approved a specification for this ticket" and "this ticket is
/// phase execution" are one fact, and a word this framework wrote onto somebody else's board only
/// to read back itself was the redundancy. A PERSON still types the phase word: no filing writes
/// it, it is a documented trigger an operator chooses, and it binds exactly as it always did.
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
    public void Routing_AHandTypedPhaseWord_BindsToPhaseExecution()
    {
        var matches = _sut.Resolve(
            TagResolved(), Envelope("proj", PhaseTicketRenderer.PhaseLabel, "bug"));

        matches.Should().ContainSingle(
            m => m.PipelineName == PipelinePresets.PhaseExecutionName,
            "no filing writes this word, but a person types it deliberately — it is a documented "
            + "trigger, it wins over the project's own label map exactly as it always did, and "
            + "taking it away would remove a way of starting a run that nobody asked to lose");
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
    public void Routing_ATicketCarryingNeither_IsRoutedByTheOperatorsOwnRules()
    {
        var matches = _sut.Resolve(TagResolved(), Envelope("proj", "bug"));

        matches.Should().ContainSingle()
            .Which.PipelineName.Should().Be("fix-bug",
                "a ticket carrying neither binding key keeps today's pipeline_from_label routing "
                + "untouched — the bind is two named keys, not a catch-all");
    }

    [Fact]
    public void Routing_ATicketWithARecordAndNoStamp_BindsToPhaseExecution()
    {
        // 2026-09-25-3c7aa: the approval RECORD, read where the envelope was built. A stamp
        // anybody with tracker access can delete is no longer what decides the route.
        var matches = _sut.Resolve(TagResolved(), Envelope("proj") with { HasApprovedRecord = true });

        matches.Should().ContainSingle()
            .Which.PipelineName.Should().Be(PipelinePresets.PhaseExecutionName,
                "the record is the durable half of the fact, and a deleted label must not cost "
                + "the ticket its route");
    }

    [Fact]
    public void Routing_ATicketWithARecordAndTheRecordLabel_IsStillRefusedFirst()
    {
        var matches = _sut.Resolve(
            TagResolved(),
            Envelope("proj", PhaseTicketRenderer.EpicLabel) with { HasApprovedRecord = true });

        matches.Should().BeEmpty(
            "a record is not work, and that refusal stays ahead of every other rule — including "
            + "the one this phase added");
    }

    [Fact]
    public void Routing_AnEnvelopeNobodyEnriched_RoutesExactlyAsBefore()
    {
        var matches = _sut.Resolve(TagResolved(), Envelope("proj", "bug"));

        matches.Should().ContainSingle()
            .Which.PipelineName.Should().Be("fix-bug",
                "an envelope built by a caller with no store behind it carries false, which is "
                + "the state every envelope was in before this field existed");
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
