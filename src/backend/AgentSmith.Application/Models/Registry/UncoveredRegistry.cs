using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models.Registry;

/// <summary>
/// A configured registry whose host the deterministic NuGet/npm fast-paths did
/// NOT stage, yet appears verbatim in the checked-out working tree.
/// <see cref="MatchingPaths"/> are the repo files the host-grep matched — they
/// ride along as context for the LLM stager; code never interprets them
/// (no manifest parsing, no ecosystem inference — p0375).
/// </summary>
public sealed record UncoveredRegistry(
    RegistryConfig Registry, IReadOnlyList<string> MatchingPaths);
