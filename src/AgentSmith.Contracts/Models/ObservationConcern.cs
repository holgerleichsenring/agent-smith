using System.Text.Json.Serialization;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// The concern area an observation addresses.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ObservationConcern
{
    Correctness,
    Architecture,
    Performance,
    Security,
    Legal,
    Compliance,
    Risk
}
