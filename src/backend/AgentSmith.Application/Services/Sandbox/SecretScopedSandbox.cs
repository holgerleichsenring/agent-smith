using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992d: a sandbox holding ONE repository at its own root (a design turn's work clone at /work), whose
/// steps speak for that repository's scoped secrets. Unlike <see cref="RepoSandbox"/> it re-roots nothing: paths and
/// working directories pass through exactly as given; only <see cref="Step.SecretScope"/> is stamped.
/// </summary>
public sealed class SecretScopedSandbox(ISandbox inner, string repoName) : ISandbox, ISandboxWrapper
{
    public ISandbox Inner => inner;

    public string JobId => inner.JobId;

    public Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken) =>
        inner.RunStepAsync(step with { SecretScope = repoName }, progress, cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
