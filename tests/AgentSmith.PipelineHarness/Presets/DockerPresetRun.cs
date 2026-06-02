using AgentSmith.PipelineHarness.Composition;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199c: bundles the three per-test docker-tier objects so a preset class
/// can hold them as one record and dispose both temp dirs + sandbox graph
/// in a single async block. Owned by the test method — the harness owns
/// the deeper service-provider lifecycle.
/// </summary>
internal sealed record DockerPresetRun(
    RealCompositionHarness Harness,
    DockerHarnessSession Session,
    PipelineRunner Runner) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await Harness.DisposeAsync();
        await Session.DisposeAsync();
    }
}
