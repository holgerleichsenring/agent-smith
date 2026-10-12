using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// Each context's Limits and baseline, read at the branch's base. A context with a rooted or
/// escaping workdir, no Limits or no baseline is not judged — a limit is green on the day it is
/// installed, and without a baseline nothing says what was there. Every normalized workdir still
/// owns its files, so a nested context's file is never judged by the context around it.
/// 2026-10-13-2d0c: read at the merge-base; a git failure is a warning, never "no Limits".
/// </summary>
public sealed class StructureGateContexts(
    StructureGitReads git, StructureLimits limits, StructureBaselineFile baselineFile,
    ILogger<StructureGateContexts> logger)
{
    public async Task<(IReadOnlyList<string> Owners, IReadOnlyDictionary<string, StructureContextRules> Rules)>
        ReadAsync(
        IReadOnlyList<RemoteContextDiscovery> contexts, ISandbox sandbox, string baseRev, CancellationToken ct)
    {
        var owners = new List<string>();
        var rules = new Dictionary<string, StructureContextRules>(StringComparer.Ordinal);
        foreach (var context in contexts)
        {
            if (StructureWorkdir.Normalize(context.Workdir) is not { } workdir)
            {
                logger.LogWarning("Structure gate skips {Context}: workdir '{Workdir}' is rooted or escapes",
                    context.ContextName, context.Workdir);
                continue;
            }
            owners.Add(workdir);
            if (await RulesAsync(sandbox, baseRev, context.ContextName, workdir, ct) is { } read)
                rules[workdir] = read;
        }
        return (owners, rules);
    }

    private async Task<StructureContextRules?> RulesAsync(
        ISandbox sandbox, string baseRev, string contextName, string workdir, CancellationToken ct)
    {
        var meta = $"{ProjectMetaPaths.Contexts}/{contextName}";
        if (await ReadAsync(sandbox, baseRev, contextName, $"{meta}/{ProjectMetaPaths.PrinciplesFile}", ct) is not { } principles)
            return null;
        var limitSet = limits.Read(principles.Content);
        if (limitSet.IsEmpty) return null;
        if (await ReadAsync(sandbox, baseRev, contextName, $"{meta}/{StructureBaselineFile.FileName}", ct) is not { } baseline)
            return null;
        if (baseline.Content is null)
        {
            logger.LogInformation("Structure gate passes {Context}: Limits but no baseline at {Base}", contextName, baseRev);
            return null;
        }
        return new StructureContextRules(workdir, limitSet, baselineFile.Index(baselineFile.Parse(baseline.Content)));
    }

    // 2026-10-13-2d0c: a git that could not answer passes the context, and says so.
    private async Task<StructureBaseRead?> ReadAsync(
        ISandbox sandbox, string baseRev, string contextName, string path, CancellationToken ct)
    {
        var read = await git.ReadAtAsync(sandbox, baseRev, path, ct);
        if (read.Failure is null) return read;
        logger.LogWarning("Structure gate passes {Context}: git could not read its terms at {Base} ({Failure})",
            contextName, baseRev, read.Failure);
        return null;
    }
}
