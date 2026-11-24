using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dg: one secret a provider writes for a PoC — its namespace and name, its Kubernetes type
/// (<see cref="PocSecretTypes"/>), its entries (each value in a redacting holder) and the target's certificate PEM.</summary>
public sealed record PocSecretOrder(
    string Namespace, string Name, string Type, IReadOnlyDictionary<string, SecretInput> Entries, string Certificate);
