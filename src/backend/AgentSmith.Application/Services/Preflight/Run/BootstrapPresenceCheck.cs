using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Preflight.Run;

/// <summary>
/// 2026-11-16-f3fba: every repository of the run carries its agent-smith bootstrap, as the host serves it,
/// before a sandbox is started — replacing BootstrapCheck and BootstrapGate, which asked the same question
/// inside the sandbox after the checkout. An incomplete repository refuses the run as bootstrap-missing;
/// one the host could not answer for refuses it as readiness-unknown, an infrastructure end.
/// </summary>
public sealed class BootstrapPresenceCheck(RepoReadiness readiness) : IRunPreflightCheck
{
    private const string Lever = "run init-project for this project and merge its pull request, then start the next attempt";

    public string Name => "bootstrap-presence";

    public bool NeedsSandbox => false;

    public async Task<RunPreflightFinding> RunAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r) && r is not null ? r : [];
        var run = RunBranchResolver.Resolve(pipeline);
        var verdicts = new List<(string Repo, RemoteBootstrapVerdict Verdict)>();
        foreach (var repo in repos)
            verdicts.Add((repo.Name, await readiness.ReadAsync(repo, BaseOf(run, repo), run?.Name, cancellationToken)));
        var incomplete = verdicts.Where(v => v.Verdict.State == RemoteBootstrapState.Incomplete).ToList();
        if (incomplete.Count > 0) return Missing(pipeline, incomplete);
        var unknown = verdicts.Where(v => v.Verdict.State == RemoteBootstrapState.Unknown).ToList();
        return unknown.Count > 0
            ? Unreadable(unknown)
            : RunPreflightFinding.Pass(Name, $"{verdicts.Count} repository(ies) carry their bootstrap");
    }

    // A slice starts from its parent's rung, every other run from the repository's default branch.
    private static BranchName? BaseOf(RunBranch? run, RepoConnection repo)
    {
        if (!string.IsNullOrWhiteSpace(run?.ParentTicketId))
            return TicketBranchNamer.Compose(new TicketId(run.ParentTicketId.Trim()));
        return string.IsNullOrWhiteSpace(repo.DefaultBranch) ? null : new BranchName(repo.DefaultBranch);
    }

    private RunPreflightFinding Missing(PipelineContext pipeline, List<(string Repo, RemoteBootstrapVerdict Verdict)> incomplete)
    {
        var project = pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var p) && p is not null ? p.Name : "";
        var message = string.Join("; ", incomplete.Select(i =>
            $"repo '{i.Repo}' has no bootstrap: {string.Join(", ", i.Verdict.Missing.Select(m => m.Describe()))}"));
        return RunPreflightFinding.Fail(Name, message, Lever) with
        {
            Kind = RunEndKinds.BootstrapMissing,
            Detail = new EndDetail(project, [.. incomplete.Select(i => i.Repo)], "Initialise", project, "/config/projects"),
        };
    }

    private RunPreflightFinding Unreadable(List<(string Repo, RemoteBootstrapVerdict Verdict)> unknown)
    {
        var reasons = string.Join("; ", unknown.Select(u => $"{u.Repo}: {u.Verdict.Reason ?? "no answer"}"));
        return RunPreflightFinding.Fail(Name, "the host did not say whether these repositories carry their bootstrap — "
            + reasons, "retry once the host answers") with
        {
            Kind = RunEndKinds.ReadinessUnknown,
            Detail = new EndDetail(reasons, [.. unknown.Select(u => u.Repo)]),
        };
    }
}
