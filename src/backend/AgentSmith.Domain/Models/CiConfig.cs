namespace AgentSmith.Domain.Models;

public sealed record CiConfig(
    bool HasCi,
    string? BuildCommand,
    string? TestCommand,
    string? CiSystem);
