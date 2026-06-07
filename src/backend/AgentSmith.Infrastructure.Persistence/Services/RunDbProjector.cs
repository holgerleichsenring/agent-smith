using System.Text.Json;
using System.Collections.Concurrent;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// Server-side single writer: projects every RunEvent the broadcaster drains
/// from the run stream into the relational store. Typed run facts go through
/// <see cref="RunEventApplier"/>; the raw event trail is BATCHED (flushed per N
/// events or on RunFinished) so the per-event payload writes don't dominate. The
/// spawned job never touches the DB — only this server-side projector does.
/// </summary>
public sealed class RunDbProjector(
    IServiceScopeFactory scopeFactory,
    RunEventApplier applier)
{
    private const int FlushThreshold = 25;
    private readonly ConcurrentDictionary<string, RunTrailBuffer> _buffers = new();

    public async Task ProjectAsync(AgentSmith.Contracts.Events.RunEvent runEvent, CancellationToken cancellationToken)
    {
        // The projector is a singleton consuming the run stream — NOT a web
        // request — so it opens a scope per event and applies the typed entity
        // through the scoped unit of work.
        using (var scope = scopeFactory.CreateScope())
            await applier.ApplyAsync(Uow(scope), runEvent, cancellationToken);

        var buffer = _buffers.GetOrAdd(runEvent.RunId, _ => new RunTrailBuffer());
        var toFlush = buffer.Add(runEvent, FlushThreshold);
        if (toFlush is not null) await FlushAsync(runEvent.RunId, toFlush, cancellationToken);
        if (runEvent.Type == EventType.RunFinished) _buffers.TryRemove(runEvent.RunId, out _);
    }

    private async Task FlushAsync(
        string runId, IReadOnlyList<(long Seq, AgentSmith.Contracts.Events.RunEvent Event)> events, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = Uow(scope);
        foreach (var (seq, ev) in events) uow.Add(BuildTrail(runId, seq, ev));
        await uow.SaveChangesAsync(ct);
    }

    private static IUnitOfWork Uow(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    private static Entities.RunEvent BuildTrail(string runId, long seq, AgentSmith.Contracts.Events.RunEvent ev) =>
        new()
        {
            RunId = runId, Seq = seq, Type = ev.Type.ToString(), Timestamp = ev.Timestamp,
            Role = RoleOf(ev), Phase = PhaseOf(ev), Repo = RepoOf(ev),
            PayloadJson = JsonSerializer.Serialize(ev, ev.GetType()),
        };

    private static string? RoleOf(AgentSmith.Contracts.Events.RunEvent ev) => ev switch
    {
        LlmCallFinishedEvent e => e.Role,
        LlmCallStartedEvent e => e.Role,
        ToolCallEvent e => e.Role,
        ToolResultEvent e => e.Role,
        _ => null,
    };

    private static string? PhaseOf(AgentSmith.Contracts.Events.RunEvent ev) => ev switch
    {
        LlmCallFinishedEvent e => e.Phase,
        ToolCallEvent e => e.Phase,
        ToolResultEvent e => e.Phase,
        _ => null,
    };

    private static string? RepoOf(AgentSmith.Contracts.Events.RunEvent ev) => ev switch
    {
        LlmCallFinishedEvent e => e.RepoName,
        ToolCallEvent e => e.RepoName,
        SandboxCreatedEvent e => e.Repo,
        PullRequestOutcomeEvent e => e.Repo,
        _ => null,
    };
}
