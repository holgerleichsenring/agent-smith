using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-11-10-8e59: the result line of a pipeline that ran to its end — the run's summary, which is what the operator
/// reads. 2026-11-18-9c1cb: no spec review stops an attempt before a spec any more, so the line is the plain one.
/// </summary>
public static class PipelineCompletedLine
{
    public const string Completed = "Pipeline completed successfully";

    public static string For(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Completed;
    }
}
