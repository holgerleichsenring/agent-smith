using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-22-9519: a conversation closes a ticket it filed that nothing ever claimed.
/// <para>
/// The two halves that make the claim true are both driven here. THE CLAIM IS ASKED TWICE: a
/// claim takes the LEASE and then enqueues, and the run ROW appears when a worker dequeues, so a
/// question that read only rows would close a ticket a worker is about to pick up. THE RECORD
/// FOLLOWS THE TRACKER: a close the tracker did not perform changes no state, sends no nudge and
/// claims nothing.
/// </para>
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class FiledTicketWithdrawalTests : IDisposable
{
    private const string Session = "s-9519";
    private const string Dialog = "d-9519";
    private const string Project = "alpha";
    private const string Key = "#4711";
    private const string TicketIdValue = "4711";
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-09-22T09:00:00Z");

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly FiledWorkWatchRegistry _registry = new();
    private readonly ClosingProvider _tracker = new(closes: true);

    public FiledTicketWithdrawalTests()
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
    public async Task Withdraw_ATicketNothingHasClaimed_IsClosedAndRecorded()
    {
        await SessionAsync(DispatcherDefaults.PlatformDashboard, Ticket(Key, TicketIdValue));

        var result = await Withdraw().WithdrawAsync(Session, Key, "filed into the wrong project", default);

        result.Withdrawn.Should().BeTrue();
        _tracker.Closed.Should().ContainSingle().Which.Ticket.Should().Be(TicketIdValue);
        _tracker.Closed.Single().Resolution.Should().Contain("filed into the wrong project");
        var start = (await FilingAsync()).Filed.Single().Start!;
        start.State.Should().Be(FiledStartState.Withdrawn);
        start.Reason.Should().Contain("filed into the wrong project");
    }

    /// <summary>
    /// The window the row reader cannot see. The lease exists from the moment a claim is taken;
    /// the run row appears only once a worker has dequeued it. Closing here would let that run
    /// write its own terminal status over the closed one — a record saying withdrawn about
    /// delivered work.
    /// </summary>
    [Fact]
    public async Task Withdraw_ATicketWhoseClaimHoldsALeaseWithNoRunRowYet_IsRefused()
    {
        await SessionAsync(DispatcherDefaults.PlatformDashboard, Ticket(Key, TicketIdValue));
        await Leases().TryClaimAsync(Project, new TicketId(TicketIdValue), default);

        var result = await Withdraw().WithdrawAsync(Session, Key, "changed my mind", default);

        result.Withdrawn.Should().BeFalse();
        result.Reason.Should().Contain("claimed").And.Contain("run controls");
        _tracker.Closed.Should().BeEmpty("a ticket a claim holds is never closed underneath it");
        (await FilingAsync()).Filed.Single().Start!.State.Should().Be(FiledStartState.NotStarted);
    }

    [Fact]
    public async Task Withdraw_ATicketWithARunRow_IsRefused()
    {
        await SessionAsync(DispatcherDefaults.PlatformDashboard, Ticket(Key, TicketIdValue));
        _context.Add(new Run
        {
            Id = "2026-09-22T08-00-00-0001", Project = Project, Pipeline = "code",
            TicketId = TicketIdValue, Status = "success", StartedAt = T,
        });
        await _context.SaveChangesAsync();

        var result = await Withdraw().WithdrawAsync(Session, Key, "changed my mind", default);

        result.Withdrawn.Should().BeFalse();
        result.Reason.Should().Contain("2026-09-22T08-00-00-0001");
        _tracker.Closed.Should().BeEmpty();
    }

    [Fact]
    public async Task Withdraw_ACloseTheTrackerDidNotPerform_LeavesTheRecordAsItWas()
    {
        await SessionAsync(DispatcherDefaults.PlatformDashboard, Ticket(Key, TicketIdValue));
        var refusing = new ClosingProvider(closes: false);

        var result = await Withdraw(refusing).WithdrawAsync(Session, Key, "no longer needed", default);

        result.Withdrawn.Should().BeFalse();
        result.Reason.Should().Contain("still open");
        refusing.Closed.Should().ContainSingle("the close was attempted — it is the ANSWER that was no");
        (await FilingAsync()).Filed.Single().Start!.State.Should().Be(
            FiledStartState.NotStarted, "a record saying withdrawn about an open ticket is the lie this prevents");
    }

    [Fact]
    public async Task Withdraw_AKeyThisConversationDidNotFile_ClosesNothing()
    {
        await SessionAsync(DispatcherDefaults.PlatformDashboard, Ticket(Key, TicketIdValue));

        var result = await Withdraw().WithdrawAsync(Session, "#9999", "wrong one", default);

        result.Withdrawn.Should().BeFalse();
        result.Reason.Should().Contain("#9999").And.Contain(Key);
        _tracker.Closed.Should().BeEmpty();
    }

    /// <summary>
    /// A filing written before filed tickets carried a display key can name nothing, and the
    /// refusal says so instead of closing whichever ticket happened to be first.
    /// </summary>
    [Fact]
    public async Task Withdraw_AFilingWithNoKeys_IsRefusedWithTheReason()
    {
        await SessionAsync(
            DispatcherDefaults.PlatformDashboard,
            new FiledTicket($"https://tracker.test/{TicketIdValue}", "Work")
            { TicketId = TicketIdValue, Project = Project });

        var result = await Withdraw().WithdrawAsync(Session, Key, "no longer needed", default);

        result.Withdrawn.Should().BeFalse();
        result.Reason.Should().Contain("display key");
        _tracker.Closed.Should().BeEmpty();
    }

    /// <summary>
    /// A design conversation runs on chat as well as on the page, and the filing is written on the
    /// turn's own platform. The session row is found by its id, which is unique — a platform named
    /// here would be a surface guessed rather than resolved, and every chat conversation's
    /// withdrawal would answer "this conversation has filed nothing".
    /// </summary>
    [Fact]
    public async Task Withdraw_AConversationOnAChatSurface_ResolvesItsOwnFiling()
    {
        await SessionAsync("slack", Ticket(Key, TicketIdValue));

        var result = await Withdraw().WithdrawAsync(Session, Key, "filed by mistake", default);

        result.Withdrawn.Should().BeTrue();
        (await FilingAsync("slack")).Filed.Single().Start!.State.Should().Be(FiledStartState.Withdrawn);
    }

    /// <summary>
    /// The pane learns without a reload. The refetch the panel does is triggered by a nudge, and
    /// the watch behind it is keyed on the TICKET — for a population defined as the tickets no run
    /// ever touched, a run-keyed announcement is a dead channel by construction.
    /// </summary>
    [Fact]
    public async Task FiledWork_AWithdrawal_NudgesTheWatchersOfThatTicket()
    {
        await SessionAsync(DispatcherDefaults.PlatformDashboard, Ticket(Key, TicketIdValue));
        _registry.Watch("c-1", [TicketIdValue]);
        var clients = new RecordingClients();

        await Withdraw(nudge: new FiledWorkNudge(Hub(clients), _registry))
            .WithdrawAsync(Session, Key, "filed by mistake", default);

        clients.Addressed.Should().ContainSingle().Which.Should().Equal("c-1");
        clients.Sent.Single().Method.Should().Be(FiledWorkNudge.Message);
    }

    /// <summary>
    /// The tool is built from the seed BESIDE the dialogue identity, exactly as ask_human is — so a
    /// turn that carries no conversation carries no withdrawal either. Without this seed the port
    /// exists and nothing can reach it.
    /// </summary>
    [Fact]
    public void SpecDialogTurn_TheSeeds_CarryTheWithdrawalUnderItsContextKey()
    {
        var withdrawal = Mock.Of<Contracts.Dialogue.IFiledTicketWithdrawal>();

        var seeds = SpecDialogTurnSeeds.Build(
            new ConversationState
            {
                JobId = Session, Project = Project, ChannelId = Dialog, UserId = "person-a",
                Platform = DispatcherDefaults.PlatformDashboard, TicketId = string.Empty, StartedAt = T,
            },
            [new RepoConnection { Name = "sample-api" }],
            new Dictionary<string, Contracts.Sandbox.ISandbox>(), new SpecDialogReplySlot(),
            DialogImageSet.None, withdrawal);

        seeds[Contracts.Commands.ContextKeys.SpecDialogWithdrawal].Should().BeSameAs(withdrawal);
        seeds[Contracts.Commands.ContextKeys.DialogueJobId].Should().Be(Session);
    }

    // ---- helpers ----

    private FiledTicketWithdrawal Withdraw(
        ITicketProvider? tracker = null, FiledWorkNudge? nudge = null) =>
        new(Scopes(), Config(), Factory(tracker ?? _tracker), new FiledWorkTrackerProjects(Config()),
            nudge, NullLogger<FiledTicketWithdrawal>.Instance);

    private ActiveRunRepository Leases() => new(
        _context, new SqliteUniqueViolationTranslator(), TimeProvider.System,
        NullLogger<ActiveRunRepository>.Instance);

    private IServiceScopeFactory Scopes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUnitOfWork>(_context);
        services.AddSingleton(new SpecDialogSessionRepository(_context));
        services.AddSingleton<SpecDialogLatestOutcomeStore>();
        services.AddSingleton(NullLogger<SpecDialogLatestOutcomeStore>.Instance);
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<SpecDialogLatestOutcomeStore>>(
            NullLogger<SpecDialogLatestOutcomeStore>.Instance);
        services.AddSingleton(sp => Leases());
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>(StringComparer.Ordinal)
        {
            [Project] = new()
            {
                Name = Project,
                DefaultPipeline = "code",
                Tracker = new TrackerConnection { Name = "sample-tracker", Type = TrackerType.GitHub },
                Repos = [new RepoConnection { Name = "sample-api" }],
            },
        },
    };

    private static ITicketProviderFactory Factory(ITicketProvider provider)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider);
        return factory.Object;
    }

    private static FiledTicket Ticket(string key, string id) =>
        new($"https://tracker.test/{id}", $"Work {id}")
        {
            TicketId = id,
            Project = Project,
            Key = key,
            Start = new FiledWorkStart(FiledStartState.NotStarted, "nothing would route it"),
        };

    private SpecDialogLatestOutcomeStore Store() =>
        new(new SpecDialogSessionRepository(_context),
            NullLogger<SpecDialogLatestOutcomeStore>.Instance);

    private async Task<SpecDialogFiling> FilingAsync(
        string platform = DispatcherDefaults.PlatformDashboard) =>
        (await Store().ReadAsync(platform, Dialog, CancellationToken.None)).Filing!;

    private async Task SessionAsync(string platform, params FiledTicket[] filed)
    {
        _context.Add(new SpecDialogSession
        {
            SessionId = Session, Platform = platform, ChannelId = Dialog, ThreadId = Dialog,
            UserId = "person-a", Project = Project, IsOpen = true, LastActivityAt = T,
        });
        await _context.SaveChangesAsync();
        await Store().SetFilingAsync(
            platform, Dialog, new FilingReport(filed, null), new AnswerOutcome(), CancellationToken.None);
    }

    private static IHubContext<JobsHub> Hub(IHubClients clients)
    {
        var context = new Mock<IHubContext<JobsHub>>();
        context.SetupGet(c => c.Clients).Returns(clients);
        return context.Object;
    }

    /// <summary>A tracker whose close says what it did, and remembers what it was asked.</summary>
    private sealed class ClosingProvider(bool closes) : ITicketProvider
    {
        public List<(string Ticket, string Resolution)> Closed { get; } = [];

        public string ProviderType => "Test";

        public Task<bool> CloseTicketAsync(
            TicketId ticketId, string resolution, CancellationToken cancellationToken)
        {
            Closed.Add((ticketId.Value, resolution));
            return Task.FromResult(closes);
        }

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ConnectionProbeResult.Reachable(0));

        public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels, string? kind,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ParentLinkResult> LinkToParentAsync(
            CreatedTicket child, TicketId parent, CancellationToken cancellationToken) =>
            Task.FromResult(ParentLinkResult.Unsupported("this fake has no relations"));

        public Task<TicketFinalizeResult> FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
            Task.FromResult(TicketFinalizeResult.Moved());
    }

    private sealed class RecordingClients : IHubClients
    {
        public List<IReadOnlyList<string>> Addressed { get; } = [];
        public List<(string Method, object?[] Args)> Sent { get; } = [];

        public IClientProxy Clients(IReadOnlyList<string> connectionIds)
        {
            Addressed.Add(connectionIds);
            return new RecordingProxy(Sent);
        }

        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excluded) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Group(string groupName) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excluded) =>
            throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class RecordingProxy(List<(string, object?[])> sent) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken)
        {
            sent.Add((method, args));
            return Task.CompletedTask;
        }
    }
}
