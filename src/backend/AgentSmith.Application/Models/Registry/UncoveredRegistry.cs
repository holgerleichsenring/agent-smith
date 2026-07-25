using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models.Registry;

/// <summary>
/// A configured registry whose host the deterministic NuGet/npm fast-paths did
/// NOT stage, yet is referenced by some other manifest/config file in the repo.
/// <see cref="HintPath"/> is a repo file that references the host, handed to the
/// LLM fallback as a starting point for detecting the ecosystem.
/// </summary>
public sealed record UncoveredRegistry(RegistryConfig Registry, string HintPath);
