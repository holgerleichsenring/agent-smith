namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Hard limits for the per-skill agentic loop. Bound from the agentsmith.yml
/// <c>limits:</c> section. Defaults match Phase B of the runtime design.
/// All limits are global; per-project overrides are intentionally out of scope.
/// </summary>
public sealed class LoopLimitsConfig
{
    public int MaxToolCallsPerSkill { get; set; } = 30;
    public int MaxToolCallsPerInvestigator { get; set; } = 10;
    public int MaxToolCallsPerVerifier { get; set; } = 20;
    public int MaxLlmCallsPerSkill { get; set; } = 15;
    public int MaxInputTokensPerSkillCall { get; set; } = 200_000;
    public int MaxOutputTokensPerSkillCall { get; set; } = 16_000;
    public int MaxSecondsPerSkillCall { get; set; } = 300;
    public int MaxConcurrentSkillCalls { get; set; } = 10;

    /// <summary>
    /// p0127b: post-LLM cap on skills selected per phase. TriageOutputProducer
    /// trims any phase whose LLM-pick exceeds this number using the activates_when
    /// specificity score (higher score wins; ties broken by skill name ascending).
    /// </summary>
    public int MaxSkillsPerPhase { get; set; } = 5;

    /// <summary>
    /// Returns the per-call tool-call cap for the active investigator mode.
    /// <c>verify_diff</c> → MaxToolCallsPerVerifier; <c>verify_hint</c> / <c>survey</c>
    /// → MaxToolCallsPerInvestigator; null/unknown → MaxToolCallsPerSkill.
    /// </summary>
    public int ResolveToolCallCap(string? investigatorMode) => investigatorMode switch
    {
        "verify_diff" => MaxToolCallsPerVerifier,
        "verify_hint" or "survey" => MaxToolCallsPerInvestigator,
        _ => MaxToolCallsPerSkill
    };
}
