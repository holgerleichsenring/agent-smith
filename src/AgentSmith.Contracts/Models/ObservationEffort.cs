using System.Text.Json.Serialization;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// Estimated effort to address an observation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ObservationEffort
{
    Small,
    Medium,
    Large
}
