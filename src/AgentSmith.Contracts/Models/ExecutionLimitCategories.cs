namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0147b: stable <see cref="SkillObservation.Category"/> values the
/// SkillCallRuntime uses when a skill call ends without producing usable
/// output. Operators see these in the pipeline summary instead of a silent
/// skill drop; downstream triage / next-round skills can branch on the
/// category string without parsing the description prose.
/// </summary>
public static class ExecutionLimitCategories
{
    /// <summary>
    /// Skill exhausted the per-call tool-call budget
    /// (<c>limits.max_tool_calls_per_skill</c> / per-investigator /
    /// per-verifier). Operator response: raise the budget for skills that
    /// genuinely need more tool turns, or narrow the prompt scope.
    /// </summary>
    public const string ExecutionLimitToolCalls = "execution-limit-tool-calls";

    /// <summary>
    /// Skill exhausted the per-call input or output token budget
    /// (<c>limits.max_input_tokens_per_skill_call</c> /
    /// <c>max_output_tokens_per_skill_call</c>). Operator response: raise
    /// the budget, simplify the prompt, or split the work.
    /// </summary>
    public const string ExecutionLimitTokens = "execution-limit-tokens";

    /// <summary>
    /// Skill exceeded the wall-clock budget
    /// (<c>limits.max_seconds_per_skill_call</c>). Operator response: raise
    /// the budget for slow tool chains, or simplify the prompt.
    /// </summary>
    public const string ExecutionLimitWallClock = "execution-limit-wall-clock";

    /// <summary>
    /// SkillCallRuntime caught an uncaught exception while invoking the skill
    /// (network, parse-after-retries, validator throw, etc.). Operator
    /// response: inspect logs for the underlying stack trace; this is the
    /// catch-all when the call never produced a usable response.
    /// </summary>
    public const string ExecutionError = "execution-error";

    /// <summary>
    /// Pipeline-level cost cap (<c>pipeline_cost_cap.default</c> or
    /// per-pipeline override) was reached and remaining LLM-driven commands
    /// (skill rounds, triage, dispatch) were skipped. Compile + Deliver
    /// still ran. Operator response: raise the cap for deep audits, or
    /// inspect which skills consumed the budget.
    /// </summary>
    public const string CostCapExhausted = "cost-cap-exhausted";

    /// <summary>
    /// Skill returned a response the observation parser could not extract
    /// any structured observations from (empty / whitespace). Recorded as a
    /// meta-observation so operators see the silent drop without polluting
    /// the severity tally with an empty-description placeholder.
    /// </summary>
    public const string ExecutionParseFailure = "execution-parse-failure";

    /// <summary>
    /// True when <paramref name="category"/> is any of the runtime-emitted
    /// execution-limit / execution-error markers. Output strategies use this
    /// to render the observation with a distinct prefix.
    /// </summary>
    public static bool IsExecutionLimit(string? category) => category is
        ExecutionLimitToolCalls
        or ExecutionLimitTokens
        or ExecutionLimitWallClock
        or ExecutionError
        or CostCapExhausted
        or ExecutionParseFailure;
}
