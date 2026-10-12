using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565d: each context's Limits and baseline, read at the branch's base. A context
/// with a rooted or escaping workdir, no Limits or no baseline is not judged — a limit is
/// green on the day it is installed, and without a baseline nothing says what was there.
/// Every normalized workdir still owns its files, so a nested context's file is never judged
/// by the context around it.
/// </summary>
public sealed class StructureGateContexts(
    StructureGitReads git, StructureLimits limits, ILogger<StructureGateContexts> logger)
{
    public async Task<(IReadOnlyList<string> Owners, IReadOnlyDictionary<string, StructureContextRules> Rules)>
        ReadAsync(
        PipelineContext pipeline, string sandboxKey, ISandbox sandbox, string baseRef, CancellationToken ct)
    {
        var owners = new List<string>();
        var rules = new Dictionary<string, StructureContextRules>(StringComparer.Ordinal);
        foreach (var context in SandboxContextList.In(pipeline, sandboxKey))
        {
            if (StructureWorkdir.Normalize(context.Workdir) is not { } workdir)
            {
                logger.LogWarning("Structure gate skips {Context}: workdir '{Workdir}' is rooted or escapes",
                    context.ContextName, context.Workdir);
                continue;
            }
            owners.Add(workdir);
            if (await RulesAsync(sandbox, baseRef, context.ContextName, workdir, ct) is { } read)
                rules[workdir] = read;
        }
        return (owners, rules);
    }

    private async Task<StructureContextRules?> RulesAsync(
        ISandbox sandbox, string baseRef, string contextName, string workdir, CancellationToken ct)
    {
        var meta = $"{ProjectMetaPaths.Contexts}/{contextName}";
        var principles = await git.ShowAsync(sandbox, baseRef, $"{meta}/{ProjectMetaPaths.PrinciplesFile}", ct);
        var limitSet = limits.Read(principles);
        if (limitSet.IsEmpty) return null;
        if (await git.ShowAsync(sandbox, baseRef, $"{meta}/{StructureBaselineFile.FileName}", ct) is not { } baseline)
        {
            logger.LogInformation(
                "Structure gate passes {Context}: Limits but no baseline at {Base}", contextName, baseRef);
            return null;
        }
        return new StructureContextRules(workdir, limitSet, StructureBaselineFile.Parse(baseline));
    }
}
