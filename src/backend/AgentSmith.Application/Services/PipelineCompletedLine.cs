using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-11-10-8e59: the result line of a pipeline that ran to its end — the run's summary, which is what the operator
/// reads. When the spec review stopped the attempt before a spec, the line says where and why, so an attempt that
/// built nothing because no reviewer answered does not read as a plain success.
/// </summary>
public static class PipelineCompletedLine
{
    public const string Completed = "Pipeline completed successfully";

    public static string For(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.TryGet<SpecSequenceStop>(ContextKeys.SpecSequenceStop, out var stop) && stop is { Park: false }
            ? $"{Completed}; stopped before {stop.BeforeSpecId}, left to the next attempt with every spec after it: {stop.Reason}"
            : Completed;
    }
}
