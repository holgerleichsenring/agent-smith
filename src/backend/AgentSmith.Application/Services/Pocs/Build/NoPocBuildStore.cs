using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>2026-11-23-933dd: without a database no PoC revision exists to build.</summary>
public sealed class NoPocBuildStore : IPocBuildStore
{
    public Task RecordPipelineRunAsync(long pocId, int revision, string? runId, string? url, CancellationToken ct) => Task.CompletedTask;

    public Task RecordImageBlockAsync(long pocId, int revision, string verdictJson, CancellationToken ct) => Task.CompletedTask;

    public Task RecordBuiltAsync(long pocId, int revision, string digest, string verdictJson, CancellationToken ct) => Task.CompletedTask;
}
