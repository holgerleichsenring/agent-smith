using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0147b: maps an Incomplete / FailedRuntime <see cref="SkillCallOutcome"/>
/// into a typed Info-severity <see cref="SkillObservation"/> so silent skill
/// drops (token / wall-clock / tool-call caps, uncaught exceptions) become
/// pipeline-visible. <see cref="SkillCallRuntime"/> calls this exactly once
/// per call when assembling the result; the obs are then appended to the
/// pipeline observation list by the round handler.
///
/// Stateless; registered as a DI singleton.
/// </summary>
public sealed class RuntimeObservationFactory
{
    /// <summary>
    /// p0151d: cost-cap-exhausted observation emitted by SkillCallRuntime when
    /// the pipeline cost cap is reached and a skill call is short-circuited.
    /// Carries the actual USD + token totals so the operator sees what the
    /// pipeline consumed before the cap fired.
    /// </summary>
    public SkillObservation BuildCostCapExhausted(string skillName, decimal usd, long tokens) =>
        new(
            Id: 0,
            Role: "runtime",
            Concern: ObservationConcern.Risk,
            Description:
                $"Skill '{skillName}' skipped: pipeline cost cap exhausted " +
                $"(${usd:F4} spent / {tokens:N0} tokens). Compile + Deliver still ran; " +
                $"raise pipeline_cost_cap in agentsmith.yml for deep audits.",
            Suggestion: "Raise pipeline_cost_cap.default (or the per-pipeline override) in agentsmith.yml.",
            Blocking: false,
            Severity: ObservationSeverity.Info,
            Confidence: 100,
            EvidenceMode: EvidenceMode.Confirmed,
            Category: ExecutionLimitCategories.CostCapExhausted);

    /// <summary>
    /// Returns a single execution-limit / execution-error observation when the
    /// call ended without usable output, or null for Ok / Failed-Parse /
    /// Failed-Validation (parse + validation failures already produce richer
    /// caller-side diagnostics via FailureReason).
    /// </summary>
    public SkillObservation? Build(
        SkillCallOutcome outcome, string skillName, string? hitLimitLabel,
        Exception? exception, string? failureReason)
    {
        var category = ResolveCategory(outcome, hitLimitLabel);
        if (category is null) return null;

        var description = BuildDescription(category, skillName, hitLimitLabel, exception, failureReason);
        var suggestion = BuildSuggestion(category);

        return new SkillObservation(
            Id: 0,
            Role: "runtime",
            Concern: ObservationConcern.Risk,
            Description: description,
            Suggestion: suggestion,
            Blocking: false,
            Severity: ObservationSeverity.Info,
            Confidence: 100,
            EvidenceMode: EvidenceMode.Confirmed,
            Category: category);
    }

    private static string? ResolveCategory(SkillCallOutcome outcome, string? hitLimitLabel)
    {
        return outcome switch
        {
            SkillCallOutcome.Incomplete => MapLimitLabel(hitLimitLabel),
            SkillCallOutcome.FailedRuntime => MapFailedRuntime(hitLimitLabel),
            _ => null
        };
    }

    /// <summary>
    /// Maps the LimitEnforcer label ("tokens" / "wall-clock") to the
    /// matching execution-limit category. When the enforcer didn't fire (label
    /// is null) and the outcome is still Incomplete, the loop terminated for
    /// a different reason — the only other path is the ME.AI tool-call cap
    /// firing inside FunctionInvokingChatClient.
    /// </summary>
    private static string MapLimitLabel(string? label) => label switch
    {
        "tokens" => ExecutionLimitCategories.ExecutionLimitTokens,
        "wall-clock" => ExecutionLimitCategories.ExecutionLimitWallClock,
        _ => ExecutionLimitCategories.ExecutionLimitToolCalls
    };

    /// <summary>
    /// FailedRuntime can mean either "wall-clock cap fired and we have no
    /// response" or "uncaught exception". The enforcer's HitLimit label
    /// disambiguates: present → cap, absent → execution-error.
    /// </summary>
    private static string MapFailedRuntime(string? hitLimitLabel)
        => hitLimitLabel is not null
            ? MapLimitLabel(hitLimitLabel)
            : ExecutionLimitCategories.ExecutionError;

    private static string BuildDescription(
        string category, string skillName, string? hitLimitLabel,
        Exception? exception, string? failureReason)
    {
        return category switch
        {
            ExecutionLimitCategories.ExecutionLimitToolCalls =>
                $"Skill '{skillName}' hit the per-call tool-call budget and stopped before completing.",
            ExecutionLimitCategories.ExecutionLimitTokens =>
                $"Skill '{skillName}' hit the per-call token budget and stopped before completing.",
            ExecutionLimitCategories.ExecutionLimitWallClock =>
                $"Skill '{skillName}' exceeded the per-call wall-clock budget and was cancelled.",
            ExecutionLimitCategories.ExecutionError =>
                $"Skill '{skillName}' failed with an uncaught runtime error: " +
                $"{exception?.Message ?? failureReason ?? "no detail"}",
            _ => $"Skill '{skillName}' ended with limit '{hitLimitLabel ?? "unknown"}'."
        };
    }

    private static string BuildSuggestion(string category) => category switch
    {
        ExecutionLimitCategories.ExecutionLimitToolCalls =>
            "Raise max_tool_calls_per_skill (or per-investigator / per-verifier) " +
            "if the skill needs more tool turns, or narrow the prompt scope.",
        ExecutionLimitCategories.ExecutionLimitTokens =>
            "Raise max_input_tokens_per_skill_call / max_output_tokens_per_skill_call, " +
            "or split the work across smaller calls.",
        ExecutionLimitCategories.ExecutionLimitWallClock =>
            "Raise max_seconds_per_skill_call for slow tool chains, or simplify the prompt.",
        ExecutionLimitCategories.ExecutionError =>
            "Inspect logs for the underlying stack trace.",
        _ => string.Empty
    };
}
