namespace AgentSmith.Contracts.Models;

/// <summary>
/// Result of an authenticated HTTP probe request executed during an active skill round.
/// </summary>
public sealed record HttpProbeResult(
    string Persona,
    string Method,
    string Url,
    int StatusCode,
    IReadOnlyDictionary<string, string> ResponseHeaders,
    string ResponseBody,
    long DurationMs);
