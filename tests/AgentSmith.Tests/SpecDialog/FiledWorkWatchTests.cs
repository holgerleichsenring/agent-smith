using System.Collections;
using System.Reflection;
using System.Security.Claims;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-042ej: who may follow a conversation's filed work, what they end up following,
/// and what a snapshot of a watched ticket sends. The ids are the SERVER's, read off the open
/// session's latest filing — a caller names a dialog id and never a ticket.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class FiledWorkWatchTests : IDisposable
{
    private const string Platform = DispatcherDefaults.PlatformDashboard;
    private const string Dialog = "d-042ej";
    private const string Owner = "person-a";
    private const string Stranger = "person-b";
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-09-17T09:00:00Z");

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly FiledWorkWatchRegistry _registry = new();

    public FiledWorkWatchTests()
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

    /// <summary>
    /// The nudge is WIRED, not merely written: deleting the call from the router leaves the
    /// whole suite green and kills the live path, so the router itself is what is driven here.
    /// </summary>
    [Fact]
    public async Task Nudge_ARoutedSnapshot_ReachesTheWatchingConnectionThroughTheRouter()
    {
        await SessionAsync(Owner, Ticket("1001"));
        await Watch().WatchAsync(Caller("c-1", Owner), Dialog);
        var clients = new RecordingClients();
        var router = new RunEventRouter(
            Mock.Of<IRunEventFanout>(), new SandboxExpansionRegistry(),
            new SandboxDetailEventClassifier(), new SandboxActivityCoalescer(),
            Mock.Of<IRunEventPersistence>(), new FiledWorkNudge(Hub(clients), _registry));

        await router.DispatchAsync(
            "2026-09-17T09-00-00-0001", Snapshot("1001"),
            new StepStartedEvent(
                "2026-09-17T09-00-00-0001", 1, "Implement", 5, T, "Implement", "AgenticMaster"),
            CancellationToken.None);

        clients.Addressed.Should().ContainSingle().Which.Should().Equal("c-1");
    }

    /// <summary>
    /// And the disconnect filter is REGISTERED: without it the registry grows one entry per
    /// connection for the life of the process, and every test stays green.
    /// </summary>
    [Fact]
    public void HubFilters_TheComposedServices_CarryThePermissionAndTheDisconnectFilter()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TokenAuthorityConfig());
        services.AddSingleton<CallerIdentityResolver>();
        services.AddDashboardApi();

        var declared = HubFilterTypeNames(services.BuildServiceProvider());

        declared.Should().Contain(nameof(HubPermissionFilter));
        declared.Should().Contain(nameof(FiledWorkDisconnectFilter));
    }

    // HubOptions.HubFilters is internal and holds a factory per AddFilter<T>() call, so the
    // filter TYPE is read off whichever member of that factory names it.
    private static IReadOnlyList<string> HubFilterTypeNames(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<HubOptions>>().Value;
        var held = typeof(HubOptions)
            .GetProperty("HubFilters", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?.GetValue(options) as IEnumerable;
        return [.. (held ?? Array.Empty<object>()).Cast<object>().Select(NameOf)];
    }

    private static string NameOf(object filter) =>
        filter.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(f => f.GetValue(filter))
            .OfType<Type>()
            .Select(t => t.Name)
            .FirstOrDefault() ?? filter.GetType().Name;

    [Fact]
    public void Watch_IsClassifiedWithDialogWriteAndRunsRead() =>
        HubMethodPermissions.For(nameof(JobsHub.WatchFiledWork))!.Names
            .Should().Equal(Permissions.DialogWrite, Permissions.RunsRead);

    [Fact]
    public async Task Watch_AnotherPrincipalsDialog_IsRefused()
    {
        await SessionAsync(Owner, Ticket("1001"));

        var refused = async () => await Watch().WatchAsync(Caller("c-1", Stranger), Dialog);

        await refused.Should().ThrowAsync<HubException>();
        _registry.WatchedBy("c-1").Should().BeEmpty();
    }

    [Fact]
    public async Task Watch_DialogIdWithNoOpenSession_WatchesNothing()
    {
        await Watch().WatchAsync(Caller("c-1", Owner), Dialog);

        _registry.WatchedBy("c-1").Should().BeEmpty("an id nobody has a conversation on follows nothing");
    }

    [Fact]
    public async Task Watch_TicketIdsComeFromTheSessionsFiling()
    {
        await SessionAsync(Owner, Ticket("1001"), Ticket("1002"));

        await Watch().WatchAsync(Caller("c-1", Owner), Dialog);

        _registry.WatchedBy("c-1").Should().BeEquivalentTo(["1001", "1002"]);
    }

    [Fact]
    public async Task Watch_Disconnect_RemovesTheEntry()
    {
        await SessionAsync(Owner, Ticket("1001"));
        await Watch().WatchAsync(Caller("c-1", Owner), Dialog);

        await new FiledWorkDisconnectFilter(_registry)
            .OnDisconnectedAsync(Lifetime("c-1"), null, (_, _) => Task.CompletedTask);

        _registry.WatchedBy("c-1").Should().BeEmpty();
        _registry.Watching("1001").Should().BeEmpty();
    }

    [Fact]
    public async Task Nudge_SnapshotForAWatchedTicket_ReachesOnlyTheWatchingConnection()
    {
        await SessionAsync(Owner, Ticket("1001"));
        await Watch().WatchAsync(Caller("c-1", Owner), Dialog);
        var clients = new RecordingClients();

        await new FiledWorkNudge(Hub(clients), _registry).OfAsync(Snapshot("1001"), default);

        clients.Addressed.Should().ContainSingle().Which.Should().Equal("c-1");
    }

    [Fact]
    public async Task Nudge_SnapshotForAnUnwatchedTicket_SendsNothing()
    {
        await SessionAsync(Owner, Ticket("1001"));
        await Watch().WatchAsync(Caller("c-1", Owner), Dialog);
        var clients = new RecordingClients();

        await new FiledWorkNudge(Hub(clients), _registry).OfAsync(Snapshot("2002"), default);

        clients.Addressed.Should().BeEmpty();
        clients.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Nudge_CarriesNoRunData()
    {
        await SessionAsync(Owner, Ticket("1001"));
        await Watch().WatchAsync(Caller("c-1", Owner), Dialog);
        var clients = new RecordingClients();

        await new FiledWorkNudge(Hub(clients), _registry).OfAsync(Snapshot("1001"), default);

        var (method, args) = clients.Sent.Single();
        method.Should().Be("FiledWorkChanged");
        args.Should().BeEmpty("the page refetches over the read that checks ownership");
    }

    [Fact]
    public async Task Nudge_SnapshotBeforeTheTicketIsKnown_SendsNothing()
    {
        await SessionAsync(Owner, Ticket("1001"));
        await Watch().WatchAsync(Caller("c-1", Owner), Dialog);
        var clients = new RecordingClients();

        // TicketId is null until FetchTicket lands on the stream.
        await new FiledWorkNudge(Hub(clients), _registry).OfAsync(Snapshot(null), default);

        clients.Addressed.Should().BeEmpty();
    }

    /// <summary>
    /// The rows are run state, so the route states runs.read beside the dialog permission: a
    /// caller holding only dialog.write is refused by the same table every other route uses.
    /// </summary>
    [Fact]
    public void FiledWork_WithoutRunsRead_IsForbidden()
    {
        var declared = ServerRouteTable
            .Facts(app => app.MapDashboardApi())
            .Single(fact =>
                fact.Method == "GET" && fact.Pattern == "/api/spec-dialog/{dialogId}/filed-work");

        declared.Permissions.Should().Equal(Permissions.DialogWrite, Permissions.RunsRead);
    }

    [Fact]
    public async Task FiledWork_AnotherPrincipalsDialog_IsForbidden()
    {
        await SessionAsync(Owner, Ticket("1001"));

        var refused = await SpecDialogViewEndpoints.ReadFiledWorkAsync(
            Dialog, Principal(Stranger), Ownership(), null!, CancellationToken.None);

        refused.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    private static RunSnapshot Snapshot(string? ticketId) =>
        RunSnapshot.Empty("2026-09-17T09-00-00-0001") with { TicketId = ticketId };

    private static IHubContext<JobsHub> Hub(IHubClients clients)
    {
        var context = new Mock<IHubContext<JobsHub>>();
        context.SetupGet(c => c.Clients).Returns(clients);
        return context.Object;
    }

    private static FiledTicket Ticket(string id) =>
        new($"https://tracker.test/{id}", $"Work {id}") { TicketId = id, Project = "alpha" };

    private FiledWorkWatch Watch() =>
        new(Ownership(), new FiledWorkFiling(Store()), _registry);

    private SpecDialogOwnership Ownership() =>
        new(new SpecDialogSessionRepository(_context));

    private SpecDialogLatestOutcomeStore Store() =>
        new(new SpecDialogSessionRepository(_context),
            NullLogger<SpecDialogLatestOutcomeStore>.Instance);

    private async Task SessionAsync(string owner, params FiledTicket[] filed)
    {
        _context.Add(new SpecDialogSession
        {
            SessionId = "s-1", Platform = Platform, ChannelId = Dialog, ThreadId = Dialog,
            UserId = owner, Project = "alpha", IsOpen = true, LastActivityAt = T,
        });
        await _context.SaveChangesAsync();
        await Store().SetFilingAsync(
            Platform, Dialog, new FilingReport(filed, null), new AnswerOutcome(),
            CancellationToken.None);
    }

    private static ClaimsPrincipal Principal(string subject) =>
        new(new ClaimsIdentity([new Claim("sub", subject)], "test"));

    private static HubCallerContext Caller(string connectionId, string subject) =>
        new FakeConnection(connectionId, Principal(subject));

    private static HubLifetimeContext Lifetime(string connectionId) =>
        new(new FakeConnection(connectionId, Principal(Owner)), null!, new JobsHub(
            null!, null!, null!, null!, null!, null!, null!, null!, null!, null!));

    /// <summary>What the nudge addressed and what it sent — the two claims, without a mock DSL.</summary>
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

    private sealed class FakeConnection(string connectionId, ClaimsPrincipal user) : HubCallerContext
    {
        public override string ConnectionId => connectionId;
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }
}
