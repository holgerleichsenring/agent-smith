using System.Text.Json.Serialization;

namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Phase of a pipeline that takes triage skill assignments.
/// AgenticStep is a pipeline step type executed between Plan and Review, not a phase that takes triage assignments.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PipelinePhase
{
    /// <summary>Lead and analysts produce the plan.</summary>
    Plan,

    /// <summary>Reviewers compare code against the plan.</summary>
    Review,

    /// <summary>Filter reduces or synthesizes findings.</summary>
    Final
}
