using System.Collections.Concurrent;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// 2026-10-15-4a4dc: the project a run belongs to, for the live hub. A project the caller already knows
/// (the live fold copies it from RunStarted) is answered as given; otherwise the run row is read. Only the
/// fan-out's live, unfinished runs are REMEMBERED — a run picked up mid-stream sends a nudge per persisted
/// event — and the fan-out forgets them when they finish, so the cache is bounded by the runs in flight.
/// Every other caller reads through and stores nothing.
/// </summary>
public sealed class RunProjectIndex(IServiceScopeFactory scopes)
{
    private readonly ConcurrentDictionary<string, string> _live = new(StringComparer.Ordinal);

    /// <summary>The run's project, or null for no row or a row naming none. Stores nothing.</summary>
    public async Task<string?> OfAsync(string runId, string? known, CancellationToken ct) =>
        Known(runId, known) ?? await ReadAsync(runId, ct);

    /// <summary>As <see cref="OfAsync"/>, remembering a project read from the row until <see cref="Forget"/>.</summary>
    public async Task<string?> LiveAsync(string runId, string? known, CancellationToken ct)
    {
        if (Known(runId, known) is { } project) return project;
        var read = await ReadAsync(runId, ct);
        if (read is not null) _live[runId] = read;
        return read;
    }

    public void Forget(string runId) => _live.TryRemove(runId, out _);

    private string? Known(string runId, string? known) =>
        !string.IsNullOrEmpty(known) ? known : _live.GetValueOrDefault(runId);

    // A host without persistence registers no unit of work: such a run names no project.
    private async Task<string?> ReadAsync(string runId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        if (scope.ServiceProvider.GetService<IUnitOfWork>() is null
            || scope.ServiceProvider.GetService<ResourceProjectReader>() is not { } reader) return null;
        var project = await reader.RunAsync(runId, ct);
        return string.IsNullOrEmpty(project) ? null : project;
    }
}
