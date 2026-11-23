namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933db: what the intake recorded on a revision. <see cref="ManifestJson"/>, the port, the
/// entrypoint and the base image are set for a validated revision; <see cref="Reason"/> for a refused one.</summary>
public sealed record PocIntakeOutcome(
    long PocId, int Revision, string RunId, string State, string? ManifestJson, int? Port, string? Entrypoint,
    string? BaseImage, string? Reason);
