using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: while PoCs are on, a configured project whose key starts with 'poc-' and that no Poc row names is
/// named on the findings channel: it stays an ordinary project, and the prefix is the PoC projects'. Republished on each
/// branch-cleanup pass (the UnanswerableParkReporter pattern), so a renamed or provisioned project stops being named
/// without anyone clearing it.
/// </summary>
internal sealed class PocPrefixFindings(IPocProjects pocProjects, IStartupFindings findings)
{
    internal const string Subsystem = "pocs";

    /// <summary>Republishes the findings; returns how many projects were named.</summary>
    public async Task<int> ReportAsync(AgentSmithConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);
        var named = new List<string>();
        if (config.Pocs.Enabled)
            foreach (var project in config.Projects.Keys.Where(k => k.StartsWith(IPocProjects.Prefix, StringComparison.OrdinalIgnoreCase)))
                if (!await pocProjects.IsAsync(project, ct)) named.Add(project);
        findings.Clear(Subsystem);
        foreach (var project in named)
            findings.Record(new StartupFinding(Subsystem, StartupFindingSeverity.Advisory,
                $"Project '{project}' starts with 'poc-' but holds no PoC; it stays an ordinary project and runs every preset. "
                + "The prefix is the one agent smith gives each person's PoC project, so a rename avoids the confusion.",
                Project: project, Field: "pocs"));
        return named.Count;
    }
}
