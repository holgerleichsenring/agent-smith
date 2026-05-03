using System.Text.Json.Serialization;

namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Role a skill can take in a pipeline run, assigned per ticket by the triage step.
/// A single skill may declare multiple supported roles (architect supports lead, analyst, reviewer);
/// triage picks one role per phase based on activation criteria.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SkillRole
{
    /// <summary>Sets the plan reviewers compare against later. One per phase.</summary>
    Lead,

    /// <summary>Contributes perspective to the lead's plan; no veto.</summary>
    Analyst,

    /// <summary>Compares actual code/diff against the plan, evidence-required.</summary>
    Reviewer,

    /// <summary>Reduces a finding list (output: list) or synthesizes a final-report artifact (output: artifact).</summary>
    Filter
}
