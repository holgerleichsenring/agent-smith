namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: <see cref="DockerfileRules"/>' verdict — the refusal, or the Dockerfile's facts.</summary>
public sealed record PocDockerfileCheck(PocRuleRefusal? Refusal, PocDockerfile? Dockerfile);
