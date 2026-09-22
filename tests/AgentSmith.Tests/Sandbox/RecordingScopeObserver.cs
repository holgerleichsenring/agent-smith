using System.Collections.Concurrent;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-17-c7aec: what a scope told the ambient observer — the line the dashboard's
/// reading channel draws. 2026-09-22-2d11b: a reused turn draws it too, which is why it is
/// worth recording twice.
/// </summary>
internal sealed class RecordingScopeObserver : ISourceScopeObserver
{
    private readonly ConcurrentQueue<(string, SourceScopeProgress)> _reports = new();

    public IReadOnlyList<(string, SourceScopeProgress)> Reports => [.. _reports];

    public Task ReportAsync(string repoName, SourceScopeProgress progress, CancellationToken ct)
    {
        _reports.Enqueue((repoName, progress));
        return Task.CompletedTask;
    }
}
