namespace AgentSmith.Application.Models.Sandbox;

/// <summary>
/// 2026-10-12-50bdb: a registry's Bearer challenge — where an anonymous token is fetched, and
/// for which service and scope.
/// </summary>
public sealed record RegistryChallenge(Uri Realm, string? Service, string? Scope);
