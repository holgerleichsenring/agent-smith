using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-10-25-9324d: the standing of every memory of one of the run's repositories, read from the ledger by the
/// repository's key. A ledger that cannot be read is logged and reads as no record: every memory is then
/// unconfirmed, which hides nothing and confirms nothing.
/// </summary>
public sealed class MemoryStandingReader(
    IMemoryEvidenceStore store, MemoryRepositoryKey keys, MemoryStandingCalculator calculator,
    ILogger<MemoryStandingReader> logger)
{
    public Task<IReadOnlyDictionary<string, MemoryStanding>> OfAsync(
        PipelineContext pipeline, string repository, CancellationToken ct) =>
        OfKeyAsync(keys.Of(pipeline, repository), repository, ct);

    /// <summary>2026-11-10-fcd7a: the standing of every memory under a ledger key — a project's store has no repository.</summary>
    public async Task<IReadOnlyDictionary<string, MemoryStanding>> OfKeyAsync(string key, string repository, CancellationToken ct)
    {
        try
        {
            return calculator.Of(await store.OfRepositoryAsync(key, ct), DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The memory standing of {Repository} ({Key}) could not be read", repository, key);
            return new Dictionary<string, MemoryStanding>(StringComparer.Ordinal);
        }
    }
}
