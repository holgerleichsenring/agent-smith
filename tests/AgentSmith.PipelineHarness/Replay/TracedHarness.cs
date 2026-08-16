using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Replay;

/// <summary>
/// p0427: switches the run trace ON for one harness composition and reads the recording
/// back — the two halves of "a run records itself, and the record can be replayed".
/// <para>
/// The switch is replaced in the container rather than via AGENTSMITH_TRACE: xUnit runs
/// collections in parallel, and a process-wide environment toggle would trace every other
/// test that happens to be composing at the same moment.
/// </para>
/// </summary>
public static class TracedHarness
{
    public static void RecordTheConversation(IServiceCollection services)
    {
        services.RemoveAll<TraceSwitch>();
        services.AddSingleton(new TraceSwitch(
            new AgentSmithConfig { Trace = new TraceConfig { Enabled = true } }));
    }

    public static Task<RecordedTrace> ReadAsync(IServiceProvider services, string runId) =>
        services.GetRequiredService<IRunTraceReader>().ReadAsync(runId, CancellationToken.None);
}
