using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933dg: opens a run's mask frame at its start with the secret variables of the PoCs its project holds, so
/// the run's trace and model calls — written on the run's flow through the one SecretMasker — replace them; a run of
/// any other project opens an empty frame. Values are read when the run starts, so a value written mid-run is masked
/// from the project's next run on. <see cref="Begin"/> is synchronous on purpose: an AsyncLocal set inside an async
/// method would end with that method instead of reaching the run.
/// </summary>
public sealed class PocRunMasking(RunMaskFrame frame, IPocMaskValues values)
{
    public Task<IReadOnlyList<string>> ValuesAsync(string project, CancellationToken ct) => values.ForProjectAsync(project, ct);

    public IDisposable Begin(IReadOnlyList<string> projectValues) => frame.Begin(projectValues);
}
