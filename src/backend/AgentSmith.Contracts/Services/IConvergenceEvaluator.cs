using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Runs the structured convergence LLM call over a set of skill observations
/// and returns a typed verdict. The handler stays oblivious to which chat
/// client / prompt / parser implements the call — those are the evaluator's
/// concern. A null verdict is never returned: parse failure surfaces as a
/// non-consensus result over the input observations so the handler can drive
/// another round.
/// </summary>
public interface IConvergenceEvaluator
{
    /// <summary>
    /// Evaluates the supplied observations and returns a populated
    /// <see cref="ConvergenceResult"/>. Cost metering is performed via the
    /// supplied <paramref name="costSink"/> callback (decoupled from
    /// <c>PipelineCostTracker</c> so the contracts layer stays clean).
    /// </summary>
    Task<ConvergenceResult> EvaluateAsync(
        AgentConfig agent,
        IReadOnlyList<SkillObservation> observations,
        Action<Microsoft.Extensions.AI.ChatResponse> costSink,
        CancellationToken cancellationToken);
}
