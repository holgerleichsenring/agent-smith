using System.Collections.Concurrent;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0423: writes the run's conversation next to its record — one row per call, keyed
/// <c>trace/0007.prompt</c>, in the artifact store the run already has.
/// <para>
/// It goes to the DATABASE, not to a file, because both spawners run the job in an
/// ephemeral container. A trace on the container's filesystem dies with the container,
/// which is precisely the case where it is needed.
/// </para>
/// <para>
/// Every entry passes the masker first. Recording never fails a run.
/// </para>
/// </summary>
public sealed class RunTraceWriter(
    IServiceScopeFactory scopeFactory,
    SecretMasker masker,
    TraceSwitch traceSwitch,
    ILogger<RunTraceWriter> logger) : IRunTraceWriter
{
    private readonly ConcurrentDictionary<string, int> _sequences = new();

    public bool IsEnabled => traceSwitch.IsOn;

    public async Task WriteAsync(
        string runId, string label, string content, CancellationToken cancellationToken)
    {
        if (!IsEnabled || string.IsNullOrEmpty(runId)) return;
        var sequence = _sequences.AddOrUpdate(runId, 1, (_, current) => current + 1);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var artifacts = scope.ServiceProvider.GetRequiredService<RunArtifactRepository>();
            await artifacts.UpsertAsync(
                runId, $"trace/{sequence:D4}.{label}", masker.Apply(content), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not record trace entry {Sequence} for run {RunId}.", sequence, runId);
        }
    }
}
