namespace AgentSmith.Infrastructure.Services.Providers.Registry;

/// <summary>2026-11-23-933de: a HEAD on a manifest — the state, the HTTP status and the Docker-Content-Digest header.</summary>
public sealed record OciManifestHead(OciManifestState State, int Status, string? Digest = null);
