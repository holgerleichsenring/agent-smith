using AgentSmith.Application.Services.Metrics;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// A board may rename the approved-set stamp (label_names: approved-set). Every reader of the
/// stamp reads it in the ticket's OWN tracker's vocabulary — a renamed stamp parks, binds and is
/// named exactly like the default one, and a word renamed on one tracker means nothing on another.
/// </summary>
public sealed class RenamedApprovedSetStampTests
{
    private const string Renamed = "ops:approved";

    private static readonly TrackerConnection RenamingTracker = new()
    {
        Name = "renaming",
        Type = TrackerType.GitHub,
        LabelNames = new Dictionary<string, string> { [TicketLabelVocabulary.ApprovedSetKey] = Renamed },
    };

    private static readonly TicketLabelVocabulary Vocabulary = TicketLabelVocabulary.For(RenamingTracker);

    [Fact]
    public void FiledTicketSpecGate_RenamedStampNoRecord_Parks()
    {
        var park = new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance).MissingSet(
            TicketWith(Renamed), new TicketKey("gh-1"), record: null,
            SpecSetBranchState.NothingAtThePath, Vocabulary);

        park.Should().NotBeNull(
            "a renamed-stamp ticket with no record deriving its own spec replaces an approved "
            + "specification with a guess");
    }

    [Fact]
    public void FiledTicketSpecGate_DefaultStampOnARenamingBoard_StillParks()
    {
        var park = new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance).MissingSet(
            TicketWith(FiledTicketLabels.ApprovedSetStamp), new TicketKey("gh-1"), record: null,
            SpecSetBranchState.NothingAtThePath, Vocabulary);

        park.Should().NotBeNull("reading is the configured name AND every name ever written");
    }

    [Fact]
    public void MissingSpecReason_RenamedStamp_NamesIt()
    {
        var reason = MissingSpecReason.For(
            TicketWith(Renamed), new TicketKey("gh-1"), record: null,
            SpecSetBranchState.NothingAtThePath, Vocabulary);

        reason.Should().Contain($"carries '{Renamed}'")
            .And.NotContain(FiledTicketLabels.ApprovedSetStamp);
    }

    [Fact]
    public void ProjectResolver_RenamedStamp_BindsToCode()
    {
        var matches = Resolver().Resolve(Config(renamingProject: "alpha"), Envelope(Renamed, "alpha"));

        matches.Should().ContainSingle().Which.PipelineName.Should().Be(PipelinePresets.CodeName);
    }

    [Fact]
    public void ProjectResolver_RenamedStampOnOtherTracker_DoesNotBind()
    {
        // Only "alpha"'s tracker renamed the stamp; "beta"'s board uses the word for routing.
        var matches = Resolver().Resolve(Config(renamingProject: "alpha"), Envelope(Renamed, "beta"));

        matches.Should().ContainSingle().Which.PipelineName.Should().Be("security-scan",
            "a word renamed on one board may be an ordinary routing word on another");
    }

    [Fact]
    public void AmendedSpecification_RenamedStamp_NoteNamesTheRenamedLabel()
    {
        var amended = AmendedSpecification.Of(
            new PhaseOutcome(new PhaseDraft("p9001", "goal", "spec: p9001\ngoal: goal", [])),
            "2026-10-06-0a0a", "job-1",
            new AmendmentRendering(
                new PhaseTicketRenderer(), new EpicChildOrderer(),
                AgentSmith.Tests.TestSupport.ApprovedSetDoubles.SeriesFiling()),
            Vocabulary);

        amended.Error.Should().BeNull();
        amended.Region.Should().Contain($"`{Renamed}`")
            .And.NotContain(FiledTicketLabels.ApprovedSetStamp,
                "the note explains the label the bound ticket carries, which a filing on this board wrote");
    }

    [Fact]
    public void Vocabulary_ForOptional_NoTrackerReadsTodaysNames()
    {
        TicketLabelVocabulary.ForOptional(null).Should().BeSameAs(TicketLabelVocabulary.Default);
        TicketLabelVocabulary.ForOptional(RenamingTracker).ApprovedSetStamp.Should().Be(Renamed);
    }

    private static ProjectResolver Resolver() => new(new AgentSmithMetrics(), new PipelineResolver());

    private static Ticket TicketWith(params string[] labels) =>
        new(new TicketId("1"), "A ticket", "Body", null, "Open", "github", labels);

    private static IncomingTicketEnvelope Envelope(string label, string project) => new()
    {
        Platform = "github",
        TicketId = "42",
        Labels = [label, project],
    };

    private static AgentSmithConfig Config(string renamingProject) => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["alpha"] = Project("alpha", renamingProject == "alpha" ? RenamingTracker : new TrackerConnection()),
            ["beta"] = Project("beta", renamingProject == "beta" ? RenamingTracker : new TrackerConnection()),
        },
    };

    private static ResolvedProject Project(string name, TrackerConnection tracker) => new()
    {
        Name = name,
        Tracker = tracker,
        GithubTrigger = new WebhookTriggerConfig
        {
            DefaultPipeline = "security-scan",
            ProjectResolution = new ProjectResolutionConfig { Strategy = ResolutionStrategy.Tag, Value = name },
        },
    };
}
