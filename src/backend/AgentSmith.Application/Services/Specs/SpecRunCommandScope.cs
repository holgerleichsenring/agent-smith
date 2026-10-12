using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0469: the agent's command log belongs to the SPEC, not to one master pass.
/// <para>
/// p0452 gave the log to the tool host, and the handler builds a new host per invocation.
/// A spliced repair pass therefore opened an empty log and published it over the first
/// pass's — the searches that proved the spec's absence claims were the ones most likely
/// to be discarded, because they run early. Opening the log through the pipeline makes the
/// second pass ACCUMULATE onto the first, and <see cref="Reset"/> ends it where p0444 ends
/// the rest of a spec's state: one spec's commands are not another's evidence.
/// </para>
/// </summary>
public static class SpecRunCommandScope
{
    /// <summary>The spec's log, created on first use. Published immediately, so the
    /// account is handed what the agent ran however the master pass ends.</summary>
    public static SpecRunCommandLog Open(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<SpecRunCommandLog>(ContextKeys.SpecRunCommands, out var open)
            && open is not null)
            return open;
        var log = new SpecRunCommandLog();
        pipeline.Set(ContextKeys.SpecRunCommands, log);
        return log;
    }

    /// <summary>Forget what the previous spec ran.</summary>
    public static void Reset(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        pipeline.Remove(ContextKeys.SpecRunCommands);
    }
}
