using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-25-8e51e: approving an amendment leaves the ticket saying what the approved set says.
/// <para>
/// WHAT IT BUYS IS A CORRECT HUMAN. A run reads the branch and never the description, so a stale
/// body drives nothing wrong — it mis-informs whoever reads the ticket, reviews the pull request
/// or judges the result. These tests assert exactly that much and no more.
/// </para>
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class TicketAmendmentTests : IDisposable
{
    private const string Session = "s-8e51e";
    private const string Project = "alpha";
    private const string Sibling = "beta";
    private const string Tracker = "sample-tracker";
    private const string Ticket4711 = "4711";
    private const string Key = "github-4711";
    private const string Directory = $".agentsmith/specs/{Key}";
    private const string Prose = "A person wrote this, and it must survive.";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;

    public TicketAmendmentTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _context.Database.Migrate();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Amendment_AfterApproval_TheTicketsFrameworkRegionRendersTheApprovedSet()
    {
        var tracker = await BoundAsync();

        var notice = await Amendment(tracker).ApplyAsync(State(), Proposal("the widget stops dropping"), default);

        notice.Should().Contain(Ticket4711).And.Contain("1 phase(s)");
        tracker.Description.Should().Contain("the widget stops dropping",
            "the region is rendered from the set that was just approved");
        tracker.Description.Should().StartWith(Prose)
            .And.EndWith("and this too.", "prose outside the markers is not the framework's to touch");
    }

    /// <summary>
    /// The record under the ticket's spec key is the one the conversation just approved — written
    /// through the recorder the dialog already owns, under the key a run of that ticket resolves.
    /// </summary>
    [Fact]
    public async Task Amendment_AfterApproval_TheApprovedSetUnderTheTicketIsTheNewOne()
    {
        var tracker = await BoundAsync();
        var store = ApprovedSetDoubles.Store();

        await Amendment(tracker, store: store).ApplyAsync(
            State(), Proposal("the widget stops dropping"), default);

        var record = await store.GetAsync(Tracker, Key, default);
        record.Should().NotBeNull();
        record!.Set.Phases.Should().ContainSingle()
            .Which.Draft.Goal.Should().Be("the widget stops dropping");
        record.Approval!.Conversation.Should().Be(Session);
    }

    /// <summary>
    /// The branch set carries a fingerprint of the ticket AS THE TRACKER STORED IT. A stale one
    /// reads as a ticket EDIT on the next run, which posts a comment telling the operator their
    /// edit was ignored — after every amendment, for ever. So the read-back happens AFTER the
    /// rewrite and the fingerprint moves with the body.
    /// </summary>
    [Fact]
    public async Task Amendment_TheBranchFingerprint_IsRefreshedSoTheNextRunReportsNoEdit()
    {
        var tracker = await BoundAsync();
        var before = TicketTextFingerprint.Of(tracker.Ticket());
        var sources = new RecordingBranchSources();

        await Amendment(tracker, sources).ApplyAsync(
            State(), Proposal("the widget stops dropping"), default);

        var written = new SpecSetIndex().FingerprintOf(
            new SpecSetIndex().Parse(sources.Writes[0].ContentOf($"{Directory}/set.yaml"))!);
        written.Should().Be(TicketTextFingerprint.Of(tracker.Ticket()),
            "the next run compares against the ticket as it stands after the amendment")
            .And.NotBe(before, "a fingerprint of the ticket before the rewrite reports an edit nobody made");
    }

    /// <summary>
    /// The lease is keyed by project and ticket, one ticket may match several projects and a run
    /// is spawned per match — so a check that looked only at the conversation's own project would
    /// amend a specification out from under a live sibling run.
    /// </summary>
    [Fact]
    public async Task Amendment_ATicketHeldByASiblingProjectsRun_IsRefusedNamingTheRun()
    {
        var tracker = await BoundAsync();
        _context.Add(new Run
        {
            Id = "2026-09-25T08-00-00-0001", Project = Sibling, Pipeline = "code",
            TicketId = Ticket4711, Status = "success",
            StartedAt = DateTimeOffset.Parse("2026-09-25T08:00:00Z"),
        });
        await _context.SaveChangesAsync();
        var sources = new RecordingBranchSources();

        var notice = await Amendment(tracker, sources).ApplyAsync(
            State(), Proposal("the widget stops dropping"), default);

        notice.Should().Contain("2026-09-25T08-00-00-0001").And.Contain("Nothing was changed");
        tracker.Regions.Should().BeEmpty("a ticket a run has taken is not rewritten underneath it");
        sources.Writes.Should().BeEmpty();
    }

    /// <summary>The window no row reader can see: the claim holds the lease and the run row it
    /// will mint does not exist yet.</summary>
    [Fact]
    public async Task Amendment_ATicketWhoseClaimHoldsOnlyALease_IsRefusedToo()
    {
        var tracker = await BoundAsync();
        await Leases().TryClaimAsync(Sibling, new TicketId(Ticket4711), default);

        var notice = await Amendment(tracker).ApplyAsync(
            State(), Proposal("the widget stops dropping"), default);

        notice.Should().Contain("claimed").And.Contain("Nothing was changed");
        tracker.Regions.Should().BeEmpty();
    }

    /// <summary>
    /// A refused rewrite — Jira's by construction, and any ticket filed before the markers —
    /// leaves the ticket, the record AND the branch exactly as they were. That is the only state
    /// a refusal may leave behind: a record moved without the ticket is the incongruence this
    /// phase exists to remove.
    /// </summary>
    [Fact]
    public async Task Amendment_ARewriteTheTrackerRefuses_LeavesTheRecordAndTheBranchAlone()
    {
        var tracker = await BoundAsync();
        tracker.Answer = TicketRewriteResult.Unsupported("Jira stores a description as a structured document.");
        var store = ApprovedSetDoubles.Store();
        var sources = new RecordingBranchSources();

        var notice = await Amendment(tracker, sources, store).ApplyAsync(
            State(), Proposal("the widget stops dropping"), default);

        notice.Should().Contain("NOT rewritten").And.Contain("structured document");
        (await store.GetAsync(Tracker, Key, default)).Should().BeNull(
            "the approved set is not moved by a rewrite that did not land");
        sources.Writes.Should().BeEmpty();
        tracker.Description.Should().NotContain("stops dropping");
    }

    /// <summary>A conversation whose ticket text was never recorded cannot address a tracker: the
    /// spec key it is bound by collapses the id and cannot be turned back into one.</summary>
    [Fact]
    public async Task Amendment_AConversationThatRecordedNoTicket_IsRefused()
    {
        var tracker = new FakeTracker(Prose);

        var notice = await Amendment(tracker).ApplyAsync(
            State(), Proposal("the widget stops dropping"), default);

        notice.Should().Contain("never recorded which ticket").And.Contain("Nothing was changed");
        tracker.Regions.Should().BeEmpty();
    }

    /// <summary>A bug ticket is filed from another renderer and carries no approved set, so there
    /// is no region of ours on the bound ticket for it to replace.</summary>
    [Fact]
    public async Task Amendment_ABugProposal_IsRefusedBeforeAnythingIsWritten()
    {
        var tracker = await BoundAsync();

        var notice = await Amendment(tracker).ApplyAsync(
            State(), new BugOutcome(new BugTicketDraft("Widget drops", "It drops.", "It stops.")), default);

        notice.Should().Contain("not filed from an approved specification");
        tracker.Regions.Should().BeEmpty();
    }

    /// <summary>
    /// The difference is COMPUTED and handed to the turn, because a prompt instruction with no
    /// computed input is a hallucination surface: a turn told to compare two texts it was not
    /// given would invent the comparison, differently every time.
    /// </summary>
    [Fact]
    public void Amendment_ATicketWhoseTextContradictsTheSet_IsComputedAndPutToThePerson()
    {
        var divergence = TicketSetDivergence.Between(
            "Title: Widget drops\n\nthe widget stops dropping",
            ["the widget stops dropping", "the widget reports why it dropped"]);

        divergence.Should().NotBeNull();
        divergence!.Phases.Should().Be(2);
        divergence.Unsaid.Should().Equal("the widget reports why it dropped");
        var rendered = Section(divergence);
        rendered.Should().Contain("the widget reports why it dropped")
            .And.Contain("ask which of the two is right")
            .And.Contain("Do not decide it yourself");
    }

    /// <summary>A ticket that already says every goal is not reported as differing — the section
    /// would be noise on every turn of a conversation whose ticket is current.</summary>
    [Fact]
    public void Amendment_ATicketThatAlreadySaysTheSet_ComputesNoDifference()
    {
        TicketSetDivergence.Between(
            "## Goal\nThe Widget   Stops Dropping\n", ["the widget stops dropping"])
            .Should().BeNull("a re-flowed, re-cased line is not a different sentence");
        TicketSetDivergence.Between("anything", []).Should().BeNull("no approved set, nothing to differ from");
    }

    // ---- helpers ----

    private static string Section(SetDivergence divergence)
    {
        var pipeline = new Contracts.Commands.PipelineContext();
        pipeline.Set(
            Contracts.Commands.ContextKeys.SpecDialogTicket,
            new SeededTicket("Widget drops", "the ticket", false, "fp", false, divergence));
        return SeededTicketSection.Render(pipeline);
    }

    private static OutcomeProposal Proposal(string goal) =>
        new PhaseOutcome(new PhaseDraft("p9001", goal, $"phase: p9001\ngoal: {goal}", []));

    /// <summary>The conversation as 8e51c leaves it: bound, with the ticket's text recorded.</summary>
    private async Task<FakeTracker> BoundAsync()
    {
        var tracker = new FakeTracker(Prose);
        tracker.Description = $"{Prose}\n\n{FramedTicketRegion.Wrap("## Goal\nthe widget drops\n")}\nand this too.";
        await new SpecDialogTicketTextRepository(_context).SaveAsync(
            new SpecDialogTicketText
            {
                SessionId = Session, TicketId = Ticket4711, Title = "Widget drops",
                Text = "Title: Widget drops\n\nthe widget drops", Fingerprint = "fp-before",
                ReadAt = DateTimeOffset.Parse("2026-09-25T07:00:00Z"),
            }, default);
        return tracker;
    }

    private TicketAmendment Amendment(
        FakeTracker tracker, RecordingBranchSources? sources = null, ISpecApprovalStore? store = null) =>
        new(Config(), Scopes(), new FiledWorkTrackerProjects(Config()),
            new SpecDialogTicketTextRepository(_context), ApprovedSetDoubles.Recorder(store),
            tracker, new PhaseTicketRenderer(), new EpicChildOrderer(),
            ApprovedSetDoubles.Branch(sources ?? new RecordingBranchSources()),
            NullLogger<TicketAmendment>.Instance);

    private ActiveRunRepository Leases() => new(
        _context, new SqliteUniqueViolationTranslator(), TimeProvider.System,
        NullLogger<ActiveRunRepository>.Instance);

    private IServiceScopeFactory Scopes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUnitOfWork>(_context);
        services.AddSingleton(sp => Leases());
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static ConversationState State() => new()
    {
        JobId = Session, ChannelId = "d-8e51e", ThreadId = "d-8e51e", UserId = "person-a",
        Platform = "dashboard", Project = Project, TicketId = string.Empty,
        Tracker = Tracker, TicketKey = Key,
        StartedAt = DateTimeOffset.Parse("2026-09-25T07:00:00Z"),
        Mode = ConversationMode.SpecDialog,
        Scope = new ActiveScope { Project = Project, Repos = ["repo-a"] },
    };

    /// <summary>Two projects on ONE tracker: a ticket matching both is spawned twice, which is
    /// why the claim question sweeps them.</summary>
    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>(StringComparer.Ordinal)
        {
            [Project] = Resolved(Project),
            [Sibling] = Resolved(Sibling),
        },
    };

    private static ResolvedProject Resolved(string name) => new()
    {
        Name = name,
        DefaultPipeline = "code",
        Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.GitHub },
        Repos = [new RepoConnection { Name = "repo-a", DefaultBranch = "main" }],
    };

    /// <summary>
    /// A tracker that stores ONE body and rewrites it the way GitHub and GitLab do — through the
    /// one region replacement all three implementing trackers call. The refusal is settable,
    /// because a refusal is as much a case as a write.
    /// </summary>
    private sealed class FakeTracker(string description) : ITicketProviderFactory
    {
        public string Description { get; set; } = description;

        public List<string> Regions { get; } = [];

        public TicketRewriteResult Answer { get; set; } = TicketRewriteResult.Ok;

        public Ticket Ticket() => new(new TicketId(Ticket4711), "Widget drops", Description, null, "Open", "GitHub");

        public ITicketProvider Create(TrackerConnection config) => new Provider(this);

        public ITicketRewriter CreateRewriter(TrackerConnection config) => new Rewriter(this);

        private sealed class Rewriter(FakeTracker owner) : ITicketRewriter
        {
            public Task<TicketRewriteResult> RewriteRegionAsync(
                TicketId ticketId, string region, CancellationToken cancellationToken)
            {
                if (!owner.Answer.Rewritten) return Task.FromResult(owner.Answer);
                if (FramedTicketRegion.Replace(owner.Description, region) is not { } rewritten)
                    return Task.FromResult(TicketRewriteResult.Unsupported("no region"));
                owner.Regions.Add(region);
                owner.Description = rewritten;
                return Task.FromResult(TicketRewriteResult.Ok);
            }
        }

        private sealed class Provider(FakeTracker owner) : ITicketProvider
        {
            private const string Inert = "An amendment reads the ticket back and writes nothing else.";

            public string ProviderType => "GitHub";

            public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
                Task.FromResult(owner.Ticket());

            public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
                throw new NotSupportedException(Inert);

            public Task<CreatedTicket> CreateAsync(
                string title, string description, IReadOnlyList<string> labels, string? kind,
                CancellationToken cancellationToken) => throw new NotSupportedException(Inert);

            public Task<ParentLinkResult> LinkToParentAsync(
                CreatedTicket child, TicketId parent, CancellationToken cancellationToken) =>
                throw new NotSupportedException(Inert);

            public Task<TicketFinalizeResult> FinalizeAsync(
                TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
                throw new NotSupportedException(Inert);
        }
    }
}
