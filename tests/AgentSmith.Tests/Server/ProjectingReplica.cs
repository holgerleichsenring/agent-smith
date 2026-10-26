using AgentSmith.Application.Services.Polling;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.Events.Projection;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-26-0f19c: one server replica as far as run events go — its own drain, its own projector
/// stack, and its own bid for the projection lease — over a Redis, a store and a lease the test shares.
/// </summary>
internal sealed class ProjectingReplica : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private Task _leading = Task.CompletedTask;

    private ProjectingReplica(JobsBroadcaster drain, ProjectionLeadership leadership, Mock<IRunEventFanout> fanout)
    {
        Drain = drain;
        Leadership = leadership;
        Fanout = fanout;
    }

    public JobsBroadcaster Drain { get; }

    public ProjectionLeadership Leadership { get; }

    public Mock<IRunEventFanout> Fanout { get; }

    public static async Task<ProjectingReplica> StartAsync(
        IConnectionMultiplexer redis, IServiceProvider provider, SharedLeaderLease lease)
    {
        var leadership = new ProjectionLeadership();
        var inner = new RunDbEventPersistence(provider.GetRequiredService<RunDbProjector>());
        var gate = new LeaderGatedRunEventPersistence(inner, leadership, new RunProjectionCatchUp(
            redis, new RunProjectionResumePoint(provider.GetRequiredService<IServiceScopeFactory>()), new EventEnvelopeSerializer()));
        var fanout = new Mock<IRunEventFanout>();
        var router = new RunEventRouter(fanout.Object, new SandboxExpansionRegistry(), new SandboxDetailEventClassifier(),
            new SandboxActivityCoalescer(), gate, new FiledWorkNudge(Mock.Of<IHubContext<JobsHub>>(), new FiledWorkWatchRegistry()));
        var drain = new JobsBroadcaster(redis, fanout.Object, router, NullLogger<JobsBroadcaster>.Instance,
            new EventEnvelopeSerializer(), provider.GetRequiredService<IRunTerminalReconciler>());
        var replica = new ProjectingReplica(drain, leadership, fanout);
        var leader = new ProjectionLeader(leadership, gate, provider.GetRequiredService<IUnfinishedRunSource>(),
            NullLogger<ProjectionLeader>.Instance);
        replica._leading = new LeaderElectedHostedService(ProjectionLeader.LeaseKey, leader.LeadAsync, lease,
            NullLogger<LeaderElectedHostedService>.Instance).RunAsync(replica._stop.Token);
        await drain.StartAsync(CancellationToken.None);
        return replica;
    }

    /// <summary>The replica goes away: its lease is released and whatever its trail buffer held is lost.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_stop.IsCancellationRequested) return;
        await _stop.CancelAsync();
        await _leading;
        await Drain.StopAsync(CancellationToken.None);
        _stop.Dispose();
    }
}
