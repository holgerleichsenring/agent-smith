using AgentSmith.PipelineHarness.Composition;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199c: bundles the per-test docker-tier objects so a preset class can
/// hold them as one record and dispose them in a single async block. The
/// optional <see cref="ApiTarget"/> is only present for passive-mode
/// api-security-scan (p0199f) — other presets leave it null.
/// </summary>
internal sealed record DockerPresetRun(
    RealCompositionHarness Harness,
    DockerHarnessSession Session,
    PipelineRunner Runner,
    StubApiTargetHost? ApiTarget = null) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await Harness.DisposeAsync();
        await Session.DisposeAsync();
        if (ApiTarget is not null) await ApiTarget.DisposeAsync();
    }
}
