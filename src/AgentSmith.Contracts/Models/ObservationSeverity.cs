using System.Text.Json.Serialization;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// Severity level of a skill observation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ObservationSeverity
{
    High,
    Medium,
    Low,
    Info
}
