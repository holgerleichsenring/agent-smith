namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: what a PoC's root Dockerfile says about the app — the one TCP port its final stage
/// exposes, its final stage's ENTRYPOINT and CMD joined into one comparable string (null when neither is there), and
/// its final FROM image.</summary>
public sealed record PocDockerfile(int Port, string? Entrypoint, string? BaseImage);
