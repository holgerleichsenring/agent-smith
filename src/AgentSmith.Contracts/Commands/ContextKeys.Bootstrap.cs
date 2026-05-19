namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Bootstrap + skill-manager + autonomous PipelineContext keys. Covers the
/// skill-manager workflow (candidates → evaluations → approval → install),
/// init-project mode flag, autonomous-pipeline findings + written-tickets,
/// query-knowledge answer, and the run-history list (consumed by autonomous-*).
/// </summary>
public static partial class ContextKeys
{
    public const string InitMode = "InitMode";

    public const string SkillCandidates = "SkillCandidates";
    public const string SkillEvaluations = "SkillEvaluations";
    public const string SkillInstallPath = "SkillInstallPath";
    public const string ApprovedSkills = "ApprovedSkills";

    public const string WikiUpdates = "WikiUpdates";
    public const string QueryAnswer = "QueryAnswer";
    public const string RunHistory = "RunHistory";
    public const string AutonomousFindings = "AutonomousFindings";
    public const string WrittenTickets = "WrittenTickets";
}
