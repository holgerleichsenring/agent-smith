using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-23-4711: after the fan-out, the tree is made to match what was derived. Rounds are
/// dispatched per DERIVED component, so a context the repository declares and the derivation
/// dropped is never visited — nothing tells it it is gone, and the repository goes on
/// declaring both it and whatever replaced it, with the gate probing both.
/// <para>
/// A derived set and a declared set are compared as SETS OF NAMES: a name on the tree side
/// only is retired, whatever replaced it. That is why this is buildable where reconciliation
/// was not — matching a dropped context onto its successor needs a key, and 2026-09-23-9bb2
/// established there is none.
/// </para>
/// </summary>
public sealed class BootstrapRetireHandler(
    BootstrapContextRetirement retirement,
    SandboxTargets sandboxTargets,
    ILogger<BootstrapRetireHandler> logger)
    : ICommandHandler<BootstrapRetireContext>
{
    public async Task<CommandResult> ExecuteAsync(
        BootstrapRetireContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
                ContextKeys.DiscoveredComponents, out var perRepo) || perRepo is not { Count: > 0 })
            return CommandResult.Ok(
                "BootstrapRetire: no derived component set — nothing is retired against nothing.");

        var retired = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var refused = new List<string>();
        foreach (var (repoName, components) in perRepo)
        {
            var sandbox = PrimarySandboxFor(context.Pipeline, repoName);
            if (sandbox is null)
            {
                logger.LogWarning(
                    "BootstrapRetire: no sandbox for repo '{Repo}' — its contexts stay as they are.",
                    repoName);
                continue;
            }
            var outcome = await retirement.RetireAsync(
                sandbox, [.. components.Select(c => c.Name)], cancellationToken);
            if (outcome.Retired.Count > 0) retired[repoName] = outcome.Retired;
            refused.AddRange(outcome.Refused.Select(r => $"{repoName}/{r}"));
        }

        if (retired.Count > 0)
            context.Pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<string>>>(
                ContextKeys.RetiredContexts, retired);
        return CommandResult.Ok(Describe(retired, refused));
    }

    /// <summary>
    /// The repository's PRIMARY sandbox — the one <see cref="InitCommitHandler"/> commits from
    /// and folds every other sandbox of that repository into. One move, in the tree that gets
    /// committed: the same rename performed in two sandboxes is a patch that cannot apply.
    /// </summary>
    private ISandbox? PrimarySandboxFor(PipelineContext pipeline, string repoName)
    {
        if (!pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos)
            || repos is null)
            return null;
        var repo = repos.FirstOrDefault(r => string.Equals(r.Name, repoName, StringComparison.Ordinal));
        if (repo is null) return null;
        var matches = sandboxTargets.SandboxesForRepo(pipeline, repo);
        return matches.Count > 0 ? matches[0].Value : null;
    }

    private static string Describe(
        IReadOnlyDictionary<string, IReadOnlyList<string>> retired, IReadOnlyList<string> refused)
    {
        var names = retired.SelectMany(kv => kv.Value.Select(c => $"{kv.Key}/{c}")).ToList();
        var message = names.Count == 0
            ? "BootstrapRetire: every context the tree carries was produced by this derivation."
            : $"BootstrapRetire: retired {names.Count} context(s) [{string.Join(", ", names)}]";
        return refused.Count == 0
            ? message
            : $"{message}; could NOT move [{string.Join(", ", refused)}] — they stay declared.";
    }
}
