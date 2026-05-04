namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Outcome of TriageOutputValidator. Errors describe why the LLM's triage output
/// is unacceptable — the producer may use this to build a stricter retry prompt.
/// </summary>
public sealed record TriageValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static TriageValidationResult Ok { get; } = new(true, Array.Empty<string>());
}
