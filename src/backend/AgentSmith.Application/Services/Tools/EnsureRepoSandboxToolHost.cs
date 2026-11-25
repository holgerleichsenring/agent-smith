using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0331: the escalation valve behind ScopeRepos' narrowing — the master calls <c>ensure_repo_sandbox(repo_name)</c>
/// when another configured repo turns out to be affected. Validates against the PROJECT config, probes capacity for
/// what the run's workspace would gain (deny = an honest tool answer, never a throw), widens ContextKeys.Repos, grows
/// the workspace through the run's LIVE coordinator, checks the source out via the SandboxRepoCloner path
/// CheckoutSource uses, and grows the master's FilesystemToolHost so the repo is addressable immediately.
/// </summary>
public sealed class EnsureRepoSandboxToolHost(
    PipelineContext pipeline,
    FilesystemToolHost fs,
    ISandboxCapacityProbe capacityProbe,
    SandboxRepoCloner cloner,
    ILogger? logger,
    SandboxTargets sandboxTargets,
    IHeldSandboxRegister heldSandboxes, Handlers.MidRunRegistryAuth? registryAuth = null) : IToolHost
{
    internal const string CapacityDenyAnswer =
        "no capacity right now — continue without this repo or ask the operator to retry later";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(EnsureRepoSandbox, name: "ensure_repo_sandbox")];
    }

    [Description("Brings another configured repository of this project into the run's workspace beside the others at /work/<repo>, addressable by name like the repos you have. " +
                 "When its toolchain is missing the workspace restarts with it: your changes are committed, pushed and come back at the same commit; build output is gone and the project's prerequisites install again. " +
                 "Call when the work needs a repository not in the run yet (a sibling a script reaches as ../<repo>, a contract both sides share). " +
                 "A capacity denial is a normal answer: continue without the repository then.")]
    public async Task<string> EnsureRepoSandbox(
        [Description("Name of the configured repository to bring into the run.")] string repo_name,
        CancellationToken ct = default)
    {
        logger?.LogInformation("tool_call: EnsureRepoSandbox repo={Repo}", repo_name);
        try
        {
            return await EnsureAsync(repo_name?.Trim() ?? string.Empty, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (WorkspaceRestoreException) { throw; } // 2026-11-25-9992b: the run has no tree to go on with
        catch (Exception ex)
        {
            // Recoverable tool error, never a run-aborting throw (p0259b contract).
            logger?.LogWarning(ex, "ensure_repo_sandbox({Repo}) failed", repo_name);
            return $"Error: could not provision a sandbox for '{repo_name}': {ex.Message}";
        }
    }

    private async Task<string> EnsureAsync(string repoName, CancellationToken ct)
    {
        if (!pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project) || project is null)
            return "Error: the project configuration is not available on this run.";
        var target = project.Repos.FirstOrDefault(r =>
            string.Equals(r.Name, repoName, StringComparison.OrdinalIgnoreCase));
        if (target is null)
            return $"Error: '{repoName}' is not a configured repo of this project. " +
                   $"Configured repos: [{string.Join(", ", project.Repos.Select(r => r.Name))}].";
        if (!pipeline.TryGet<IPipelineSandboxCoordinator>(ContextKeys.SandboxCoordinator, out var coordinator)
            || coordinator is null)
            return "Error: no live sandbox coordinator on this run.";

        var repos = pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        var inScope = repos.Any(r => string.Equals(r.Name, target.Name, StringComparison.OrdinalIgnoreCase));
        var existing = sandboxTargets.SandboxesForRepo(pipeline, target);
        if (inScope && existing.Count > 0)
        {
            GrowToolHost(target.Name, existing);
            return $"Repo '{target.Name}' is already available in this run " +
                   $"(sandboxes: {string.Join(", ", existing.Select(kv => kv.Key))}). " +
                   $"Address its paths as '{target.Name}/<path>'.";
        }

        // 2026-11-25-9992b: only the containers the workspace would gain are probed — a repository whose images it
        // serves is cloned beside the others and takes no new capacity; one that needs a new image restarts it bigger.
        var added = await coordinator.AddedToolchainsAsync(project, pipeline, target, ct);
        if (added.Count > 0 && await DeniedAsync(new RunFootprint(Sandboxes: added), ct) is { } reason)
        {
            logger?.LogInformation("ensure_repo_sandbox({Repo}) denied for capacity: {Reason}", target.Name, reason);
            return CapacityDenyAnswer;
        }

        return await SpawnAndCheckoutAsync(coordinator, project, target, repos, inScope, existing, ct);
    }

    // 2026-09-22-2d11a: release held sandboxes before the probe; 2026-09-24-81ea: only when it is short.
    private async Task<string?> DeniedAsync(RunFootprint footprint, CancellationToken ct)
    {
        await heldSandboxes.EvictIfShortAsync(async c => (await capacityProbe.HasCapacityAsync(footprint, c)).Admitted, ct);
        var capacity = await capacityProbe.HasCapacityAsync(footprint, ct);
        return capacity.Admitted ? null : capacity.Reason ?? "no capacity";
    }

    private async Task<string> SpawnAndCheckoutAsync(
        IPipelineSandboxCoordinator coordinator, ResolvedProject project, RepoConnection target,
        IReadOnlyList<RepoConnection> repos, bool inScope,
        IReadOnlyList<KeyValuePair<string, ISandbox>> existing, CancellationToken ct)
    {
        if (!inScope)
            pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [.. repos, target]);
        try
        {
            var before = existing.Select(kv => kv.Key).ToHashSet(StringComparer.Ordinal);
            await coordinator.EnsureSandboxesAsync(project, pipeline, ct);
            var mine = sandboxTargets.SandboxesForRepo(pipeline, target);
            var created = mine.Where(kv => !before.Contains(kv.Key)).ToList();
            if (created.Count > 0)
            {
                var checkout = await cloner.CheckoutIntoSandboxesAsync(
                    target, RunBranchResolver.Resolve(pipeline), created, ct);
                if (checkout.Repository is null)
                    return $"Error: a sandbox for '{target.Name}' was created but the source checkout " +
                           $"failed ({checkout.Problem}) — do not use this repo; continue without it.";
                // 2026-09-13-a284: a repo escalated mid-run gets its pull request from the
                // same loop as every other one, so it records its rung the same way.
                PullRequestTargets.Record(pipeline, target.Name, checkout.Rung);
                foreach (var (key, sandbox) in registryAuth is null ? [] : created) await registryAuth!.StageAsync(pipeline, key, sandbox, ct); // 9c1cd
            }
            GrowToolHost(target.Name, mine);
            return $"Sandbox ready for repo '{target.Name}' " +
                   $"(sandboxes: {string.Join(", ", mine.Select(kv => kv.Key))}). " +
                   $"Its source is checked out; address paths as '{target.Name}/<path>' and pass " +
                   $"repo='{target.Name}' to run_command.";
        }
        catch
        {
            // Failed escalations must not leave a repo in scope that has no sandbox —
            // CommitAndPR/WriteRunResult iterate ContextKeys.Repos.
            if (!inScope) pipeline.Set(ContextKeys.Repos, repos);
            throw;
        }
    }

    private void GrowToolHost(string repoName, IEnumerable<KeyValuePair<string, ISandbox>> sandboxes)
    {
        foreach (var (key, sandbox) in sandboxes)
            fs.AddSandbox(key, repoName, sandbox);
    }
}
