using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0427: reads a recorded run back out of the artifact store the writer put it in — the
/// same keys, parsed by the same key format, in the order the run produced them.
/// </summary>
public sealed class RunTraceReader(IServiceScopeFactory scopeFactory) : IRunTraceReader
{
    public async Task<RecordedTrace> ReadAsync(string runId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(runId)) return RecordedTrace.Empty;
        using var scope = scopeFactory.CreateScope();
        var artifacts = scope.ServiceProvider.GetRequiredService<RunArtifactRepository>();
        var rows = await artifacts.ListAsync(runId, RecordedTraceKey.Prefix, cancellationToken);
        return RecordedTrace.Of(Parse(rows));
    }

    private static IEnumerable<RecordedTraceEntry> Parse(
        IEnumerable<(string Kind, string Content)> rows)
    {
        foreach (var (kind, content) in rows)
            if (RecordedTraceKey.TryParse(kind, content, out var entry))
                yield return entry;
    }
}
