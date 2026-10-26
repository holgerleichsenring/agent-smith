namespace AgentSmith.Server.Models;

/// <summary>2026-10-26-bdd0e: how a derivation stores its specs: branch (the default) or preview.</summary>
public sealed record DeriveSpecsRequest(string? Mode = null);
