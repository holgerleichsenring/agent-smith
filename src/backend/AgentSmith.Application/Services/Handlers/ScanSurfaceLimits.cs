using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// The limits the OBSERVATION-SCHEMA surface runs under, applied to a request that was
/// otherwise shaped for the coding surface.
/// <para>
/// 2026-09-01-6c32: the closing answer is a findings array, not a coding turn, and the
/// schema's per-finding allowance means a few dozen findings do not fit in the primary
/// role's output budget.
/// </para>
/// <para>
/// 2026-09-01-7df4: the iteration ceiling is a number somebody chose — a null ceiling used
/// to fall through to the chat-client factory's private default, the fallback for a call
/// with no opinion, on the one surface whose job is to look at a lot of code. Raising it is
/// only safe on a surface that can REDUCE, so the assumed input window and the compaction
/// settings travel with it; the effective ceiling is recorded on the run, because the next
/// argument about that number should start from a measurement.
/// </para>
/// </summary>
public static class ScanSurfaceLimits
{
    public static AgenticLoopRequest Apply(
        AgenticLoopRequest request, AgentConfig config, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(pipeline);
        pipeline.Set(ContextKeys.ScanMasterIterationCeiling, config.ScanMasterLoopIterations);
        return request with
        {
            MaxOutputTokensOverride = config.ScanMasterMaxOutputTokens,
            MaxIterations = config.ScanMasterLoopIterations,
            Compaction = config.Compaction,
            ContextWindowTokensOverride = config.ScanContextWindowTokens,
        };
    }
}
