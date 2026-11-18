using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// 2026-11-18-c8e2a: every child a run's masters started, by id, and the notices of the children that finished, which
/// the master reads with its next tool result. A child never outlives its master's loop: <see cref="StopAsync"/> stops
/// what still runs and waits for it, and the handler calls it before it reads what the loop changed. 2026-11-18-c8e2d:
/// the team lives for the run (<see cref="GetOrCreate"/>), so a spec's repair pass continues the child that built it.
/// </summary>
public sealed class SubAgentTeam(ILogger logger) : IAsyncDisposable
{
    /// <summary>The heading a finished child's notice reaches its lead under.</summary>
    public const string NoticeHeading = "A sub-agent finished";

    private readonly ConcurrentDictionary<string, SubAgentMember> _members = new(StringComparer.Ordinal);
    private CancellationTokenSource _stopping = new();

    public SubAgentInbox Notices { get; } = new();

    /// <summary>The run's team, made on first use; a live handle the checkpoint leaves out.</summary>
    public static SubAgentTeam GetOrCreate(Contracts.Commands.PipelineContext pipeline, ILogger logger)
    {
        if (pipeline.TryGet<SubAgentTeam>(Contracts.Commands.ContextKeys.SubAgentTeam, out var team) && team is not null) return team;
        team = new SubAgentTeam(logger);
        pipeline.Set(Contracts.Commands.ContextKeys.SubAgentTeam, team);
        return team;
    }

    /// <summary>Cancelled when the team stops its running children.</summary>
    public CancellationToken Stopping => _stopping.Token;

    public SubAgentMember Enlist(SubAgentSpec spec)
    {
        var member = SubAgentMember.For(spec);
        _members[member.Id] = member;
        return member;
    }

    /// <summary>A child by its id, or (2026-11-18-c8e2d) by its name when exactly one child carries it.</summary>
    public SubAgentMember? Find(string idOrName)
    {
        var key = idOrName.Trim();
        if (_members.TryGetValue(key, out var member)) return member;
        var named = _members.Values.Where(m => string.Equals(m.Name, key, StringComparison.Ordinal)).ToList();
        return named.Count == 1 ? named[0] : null;
    }

    public IReadOnlyList<SubAgentMember> Members => [.. _members.Values.OrderBy(m => m.Name, StringComparer.Ordinal)];

    public IReadOnlyList<SubAgentMember> Running => [.. Members.Where(m => m.IsRunning)];

    /// <summary>Returns once a notice waits or no child runs.</summary>
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        while (Notices.Count == 0)
        {
            var running = Running.Select(m => m.Pass).ToList();
            if (running.Count == 0) return;
            await Task.WhenAny(running).WaitAsync(cancellationToken);
        }
    }

    /// <summary>Stops every child still running and waits until each has ended; the team can start children again.</summary>
    public async Task StopAsync()
    {
        var running = Running;
        if (running.Count == 0) return;
        logger.LogInformation("Stopping {Count} sub-agent(s) still running when their master's loop ended: {Names}",
            running.Count, string.Join(", ", running.Select(m => m.Name)));
        var stopping = Interlocked.Exchange(ref _stopping, new CancellationTokenSource());
        await stopping.CancelAsync();
        try
        {
            await Task.WhenAll(running.Select(m => m.Pass));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "A stopped sub-agent ended with an exception");
        }
        stopping.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _stopping.Dispose();
    }
}
