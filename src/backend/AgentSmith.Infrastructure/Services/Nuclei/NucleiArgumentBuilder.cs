using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Services.Nuclei;

/// <summary>
/// Builds the Nuclei command line from its configuration, the way
/// <see cref="Zap.ZapArgumentBuilder"/> does for ZAP. The target list and the results
/// file are named against {work}, which the tool runner resolves.
/// </summary>
internal static class NucleiArgumentBuilder
{
    internal static List<string> BuildArguments(NucleiConfig config) =>
    [
        "-list", "{work}/targets.txt",
        "-jsonl",
        "-output", "{work}/results.jsonl",
        "-severity", config.Severity,
        "-tags", config.Tags,
        "-exclude-tags", config.ExcludeTags,
        "-follow-redirects",
        "-no-interactsh",
        "-timeout", config.Timeout.ToString(),
        "-retries", config.Retries.ToString(),
        "-no-mhe",
        "-concurrency", config.Concurrency.ToString(),
        "-rate-limit", config.RateLimit.ToString(),
    ];
}
