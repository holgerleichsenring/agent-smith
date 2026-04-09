namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Declares a skill's role within a typed orchestration pipeline.
/// </summary>
public enum SkillRole
{
    /// <summary>Analyzes input, appends to shared context, no blocking.</summary>
    Contributor,

    /// <summary>Produces a plan/directive that all subsequent skills receive.</summary>
    Lead,

    /// <summary>Blocks pipeline progression; has veto authority via verdict or filtered list.</summary>
    Gate,

    /// <summary>Acts in the world; produces an artifact (report, PR, file).</summary>
    Executor,
}
