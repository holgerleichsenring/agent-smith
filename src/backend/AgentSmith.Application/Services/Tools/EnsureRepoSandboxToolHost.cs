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
/// p0331: the escalation valve behind ScopeRepos' conservative narrowing — the
/// master calls <c>ensure_repo_sandbox(repo_name)</c> when it discovers mid-run
/// that another configured repo is affected. Validates against the PROJECT
/// config (not the narrowed scope), runs its OWN single-sandbox capacity probe
/// (deny = an honest tool answer, never a throw), widens ContextKeys.Repos,
/// spawns through the run's LIVE coordinator (idempotent per group), checks the
/// source out via the same SandboxRepoCloner path CheckoutSource uses, and
/// grows the running master's FilesystemToolHost so the new repo is addressable
/// immediately.
/// </summary>
public sealed class EnsureRepoSandboxToolHost(
    PipelineContext pipeline,
    FilesystemToolHost fs,
    ISandboxCapacityProbe capacityProbe,
    ISandboxResourceResolver resourceResolver,
    SandboxRepoCloner cloner,
    ILogger? logger) : IToolHost
{
    internal const string CapacityDenyAnswer =
        "no capacity right now — continue without this repo or ask the operator to retry later";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(EnsureRepoSandbox, name: "ensure_repo_sandbox")];
    }

    [Description("Provisions a sandbox for another configured repository of this project mid-run, " +
                 "checks its source out, and makes it addressable by name (like the repos you already " +
                 "have). Call when the ticket turns out to affect a repository that is not in this " +
                 "run's scope. A capacity denial is a normal answer — continue without the repo then.")]
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
        var existing = SandboxTargets.SandboxesForRepo(pipeline, target);
        if (inScope && existing.Count > 0)
        {
            GrowToolHost(target.Name, existing);
            return $"Repo '{target.Name}' is already available in this run " +
                   $"(sandboxes: {string.Join(", ", existing.Select(kv => kv.Key))}). " +
                   $"Address its paths as '{target.Name}/<path>'.";
        }

        // Own single-sandbox probe — pipeline-aware size, orchestrator already running.
        var pipelineName = pipeline.TryGet<string>(ContextKeys.PipelineName, out var pn) ? pn : null;
        var footprint = new RunFootprint(
            Orchestrator: null, Sandboxes: [resourceResolver.Resolve(project, pipelineName)]);
        var capacity = await capacityProbe.HasCapacityAsync(footprint, ct);
        if (!capacity.Admitted)
        {
            logger?.LogInformation(
                "ensure_repo_sandbox({Repo}) denied for capacity: {Reason}", target.Name, capacity.Reason);
            return CapacityDenyAnswer;
        }

        return await SpawnAndCheckoutAsync(coordinator, project, target, repos, inScope, existing, ct);
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
            var mine = SandboxTargets.SandboxesForRepo(pipeline, target);
            var created = mine.Where(kv => !before.Contains(kv.Key)).ToList();
            if (created.Count > 0)
            {
                var repo = await cloner.CheckoutIntoSandboxesAsync(target, ResolveBranch(pipeline), created, ct);
                if (repo is null)
                    return $"Error: a sandbox for '{target.Name}' was created but the source checkout " +
                           "failed — do not use this repo; continue without it.";
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

    // Same branch resolution CheckoutSourceContextBuilder uses for the initial checkout.
    private static BranchName? ResolveBranch(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.CheckoutBranch, out var b) && !string.IsNullOrWhiteSpace(b)
            ? new BranchName(b)
            : pipeline.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId) && ticketId is not null
                ? TicketBranchNamer.Compose(ticketId)
                : null;

    private void GrowToolHost(string repoName, IEnumerable<KeyValuePair<string, ISandbox>> sandboxes)
    {
        foreach (var (key, sandbox) in sandboxes)
            fs.AddSandbox(key, repoName, sandbox);
    }
}
