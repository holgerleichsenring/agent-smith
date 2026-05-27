namespace AgentSmith.Contracts.Models;

/// <summary>
/// Result of evaluating a skill candidate for fit and safety.
/// </summary>
public sealed record SkillEvaluation(
    SkillCandidate Candidate,
    int FitScore,
    int SafetyScore,
    string FitReasoning,
    string SafetyReasoning,
    string Recommendation,
    bool HasOverlap,
    string? OverlapWith);
