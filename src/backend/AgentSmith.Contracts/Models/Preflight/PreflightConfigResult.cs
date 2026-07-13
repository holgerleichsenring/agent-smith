using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Models.Preflight;

/// <summary>
/// p0324: the one config-load attempt every check shares. A broken agentsmith.yml
/// must fail the config-schema check with the parse error — not crash DI while the
/// check classes are being constructed — so the load is deferred behind this result:
/// <see cref="Config"/> is null iff loading threw, and <see cref="LoadError"/> then
/// carries the reason.
/// </summary>
public sealed record PreflightConfigResult(
    AgentSmithConfig? Config,
    string ConfigPath,
    string? LoadError);
