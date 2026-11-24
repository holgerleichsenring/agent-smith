using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dg: a PoC's secret variables as the sealing step receives them — each value in a redacting
/// holder, and the names whose stored value does not open, with the reason.</summary>
public sealed record PocVariableValues(
    IReadOnlyDictionary<string, SecretInput> Values, IReadOnlyDictionary<string, string> Unreadable)
{
    public static PocVariableValues None { get; } = new(new Dictionary<string, SecretInput>(), new Dictionary<string, string>());
}
