using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>
/// 2026-11-23-933dd: starts a build run with the inputs both stages decide alike by — the advisories overruled at
/// either stage as trivy_ignore, the Trivy secret rules whose catalog pattern reaches pocs.scan.secret_min_confidence
/// as secret_rules — and stores the run id and page before anything reads it. A host that started the build without
/// naming it (GitHub's 204) cannot be followed, and a host's refusal is said in its words.
/// </summary>
public sealed class PocBuildStart(
    PocBuildInputs inputs, PocTrivyIgnore ignore, PocTrivySecretRules secretRules, Contracts.Pocs.IPocBuildStore store,
    ILogger<PocBuildStart> logger)
{
    public async Task<(string? RunId, string? Refused)> StartAsync(IPipelineRunner runner, PocBuildJob job, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(job);
        var (rules, failure) = secretRules.Blocking(job.Scan.SecretMinConfidence);
        if (failure is not null) return (null, $"The build of '{job.Subject.PocName}' was not started: {failure}.");
        PipelineRunStart started;
        try { started = await runner.StartAsync(inputs.Order(job, ignore.Of(job.Subject), rules), ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The host refused to start the build of PoC {PocId}", job.Subject.PocId);
            return (null, $"The host refused to start the build of '{job.Subject.PocName}': {ex.Message}");
        }
        if (started.RunId is null)
            return (null, "The host started the build without naming it, so it cannot be followed (workflow dispatch without "
                + $"return_run_details); its runs are at {started.Url}.");
        await store.RecordPipelineRunAsync(job.Subject.PocId, job.Subject.Revision, started.RunId, started.Url, ct);
        return (started.RunId, null);
    }
}
