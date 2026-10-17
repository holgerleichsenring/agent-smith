using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Loop.Sanity;

/// <summary>
/// 2026-10-17-a196a: the writes one master step was refused, for its step result. A master and its
/// children write it concurrently, so it is one locked list, opened fresh when the step composes its tools.
/// </summary>
public sealed class RefusedWriteLog
{
    private readonly Lock _sync = new();
    private readonly List<string> _refused = [];

    /// <summary>Puts a fresh log into the pipeline — the start of a master step.</summary>
    public static RefusedWriteLog OpenFresh(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var log = new RefusedWriteLog();
        pipeline.Set(ContextKeys.RefusedWrites, log);
        return log;
    }

    /// <summary>The step's log, or a fresh one when no step opened one.</summary>
    public static RefusedWriteLog Of(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<RefusedWriteLog>(ContextKeys.RefusedWrites, out var log) && log is not null
            ? log
            : OpenFresh(pipeline);
    }

    public void Record(string refusal)
    {
        lock (_sync) _refused.Add(refusal);
    }

    public IReadOnlyList<string> Refused
    {
        get { lock (_sync) return [.. _refused]; }
    }
}
