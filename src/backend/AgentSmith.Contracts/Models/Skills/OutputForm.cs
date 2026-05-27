using System.Text.Json.Serialization;

namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Shape of a role's output. Reviewers/analysts produce List, lead produces Plan,
/// filter produces List or Artifact depending on whether it reduces or synthesizes.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OutputForm
{
    /// <summary>JSON array of skill-observation objects.</summary>
    List,

    /// <summary>Single structured plan object that reviewers compare against.</summary>
    Plan,

    /// <summary>Single synthesized artifact (e.g. final security report from chain-analyst).</summary>
    Artifact
}
