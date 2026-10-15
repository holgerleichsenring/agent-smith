using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.Lifecycle;
using AgentSmith.Tests.Server.Auth;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-15-4a4dc: a deletion nudges the lists of the people who may read the deleted run's project.
/// The project is read BEFORE the row goes; the bulk clear's data-free nudge reaches every project group.
/// </summary>
public sealed class RunDeletionNudgeTests : IDisposable
{
    private readonly HubProjectStore _store = new();
    private readonly RecordingHubContext _hub = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task RunDeletion_DeleteOne_NudgesTheDeletedRunsProjectGroup()
    {
        await _store.SeedAsync(async db =>
        {
            db.Add(new Run
            {
                Id = "r-gone", Project = "alpha", Pipeline = "fix-bug", TicketId = "T-1", Status = "succeeded",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5), FinishedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        });
        await using var scope = _store.Services.CreateAsyncScope();

        await RunDeletionEndpoints.DeleteRunAsync("r-gone", Deleter(scope), Audience(), _store.Index(), _hub.Hub, CancellationToken.None);

        _hub.Messages.Should().ContainSingle().Which.Groups.Should().Equal(HubGroups.Overview, HubGroups.OverviewOf("alpha"));
    }

    [Fact]
    public async Task RunDeletion_ClearTerminal_NudgesEveryConfiguredProjectGroup()
    {
        await using var scope = _store.Services.CreateAsyncScope();

        await RunDeletionEndpoints.ClearTerminalAsync(Deleter(scope), Audience(), _hub.Hub, "terminal", ProjectScopeUnderTest.Nobody(),
            new AgentSmith.Server.Security.CallerProjectScope(ProjectScopeUnderTest.Access(new TokenAuthorityConfig())), CancellationToken.None);

        _hub.Messages.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Groups = new[] { HubGroups.Overview, HubGroups.OverviewOf("alpha"), HubGroups.OverviewOf("beta") },
            Arguments = new object[] { "bulk" },
        });
    }

    // A terminal run never reaches the force-clear, so the lease, queue and finalizer stay untouched.
    private static RunDeleter Deleter(AsyncServiceScope scope)
    {
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return new RunDeleter(
            new RunRepository(uow), new RunDeletionRepository(uow), Mock.Of<IActiveRunLease>(),
            Mock.Of<ICapacityQueue>(), null!, NullLogger<RunDeleter>.Instance);
    }

    private OverviewAudience Audience() => new(
        new TokenAuthorityConfig { Enforce = true }, _store.Index(), ProjectScopeUnderTest.Projects("alpha", "beta"));
}
