using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Commits changes per repo (in the sandbox where the modifications live) and
/// opens a pull request via the source provider's API. Per repo: detect staged
/// changes (skip if none), commit + push, open PR with a sibling-PR marker in
/// the body that p0158c's PATCH pass replaces with actual sibling URLs. Each
/// outcome is recorded in ContextKeys.OpenedPullRequests; single-PR runs also
/// publish ContextKeys.PullRequestUrl for backward compatibility with the
/// pipeline executor result adapter. Ticket lifecycle finalization runs after
/// all repos have been processed and references the primary PR URL.
/// </summary>
public sealed class CommitAndPRHandler(
    ISourceProviderFactory sourceFactory,
    ITicketProviderFactory ticketFactory,
    SandboxGitOperations gitOps,
    ISecretPatternScanner secretScanner,
    IEventPublisher events,
    ILogger<CommitAndPRHandler> logger)
    : ICommandHandler<CommitAndPRContext>
{
    private const string SiblingMarker = "<!-- agentsmith:sibling-prs -->";
    // p0234: the run record (plan.md / result.md / decisions.md / context.yaml)
    // lives under this dir; force-staged so a .gitignore can't drop it.
    private const string AgentSmithRunRecordPath = ".agentsmith";

    public async Task<CommandResult> ExecuteAsync(
        CommitAndPRContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Creating PRs for ticket {Ticket} across {Repos} repo(s)...",
            context.Ticket.Id, context.Configs.Count);

        if (!context.Pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes, out var sandboxes) || sandboxes is null)
            return CommandResult.Fail("CommitAndPR requires Sandboxes published by PipelineSandboxCoordinator.");

        // p0235: stage every repo first and detect which carry a REAL code
        // change (staged paths outside .agentsmith) vs only the run-record. Open
        // a PR for each changed repo; if NONE changed, open exactly ONE record
        // PR (the first repo) carrying result.md — never empty per-repo PRs. The
        // operator's rule: at least one PR (≥ result.md), no obscure splitting.
        var stagedRepos = new List<(RepoConnection Repo, ISandbox Sandbox, bool HasCode)>();
        var opened = new List<OpenedPullRequest>(context.Configs.Count);
        var bodies = new Dictionary<string, string>(context.Configs.Count, StringComparer.Ordinal);
        foreach (var repo in context.Configs)
        {
            var matches = SandboxTargets.SandboxesForRepo(context.Pipeline, repo);
            if (matches.Count == 0)
            {
                opened.Add(new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed, "no sandbox available"));
                logger.LogWarning("{Repo}: no sandbox available", repo.Name);
                continue;
            }
            var sandbox = matches[0].Value;
            // p0299: a mixed-stack monorepo has one clone per toolchain sandbox; fold every
            // OTHER sandbox's edits into the primary so nothing is dropped at commit time.
            var consolidated = await gitOps.ConsolidateSecondarySandboxesAsync(matches, sandbox, cancellationToken);
            if (consolidated > 0)
                logger.LogInformation(
                    "{Repo}: consolidated {N} secondary sandbox(es) into {Key}",
                    repo.Name, consolidated, matches[0].Key);
            await gitOps.StageAllAsync(sandbox, cancellationToken);
            var staged = await gitOps.GetStagedFileNamesAsync(sandbox, cancellationToken);
            var hasCode = staged.Any(n => !RunRecordPaths.IsRunRecordPath(n));
            // p0249: name the resolved sandbox key + the staged set per repo. A
            // "recorded edits but committed nothing" run is otherwise a silent
            // mismatch; this line tells us WHICH sandbox the commit looked at and
            // exactly what git saw staged there.
            logger.LogInformation(
                "{Repo}: commit-sandbox key={Key} (of {N}) hasCode={HasCode} staged=[{Staged}]",
                repo.Name, matches[0].Key, matches.Count, hasCode, string.Join(", ", staged));
            stagedRepos.Add((repo, sandbox, hasCode));
        }

        var anyCode = stagedRepos.Any(s => s.HasCode);

        // p0300c: evaluate the outcome keystone BEFORE opening PRs so a
        // verification-red run opens its PR(s) as DRAFT (visible for review, not
        // mergeable) instead of a normal PR that reads as a green, ready change.
        // Same inputs the post-loop gate uses — hoisted, not duplicated.
        var pipelineName = context.Pipeline.TryGet<string>(ContextKeys.PipelineName, out var pn) && pn is not null
            ? pn : string.Empty;
        var verification = context.Pipeline.TryGet<MasterVerification>(ContextKeys.MasterVerification, out var mv)
            ? mv : null;
        var realCodeChanges = context.Changes.Count(c => !RunRecordPaths.IsRunRecordPath(c.Path.ToString()));
        var criteria = context.Pipeline.TryGet<RatifiedExpectation>(ContextKeys.RunExpectation, out var exp)
            && exp is not null ? exp.Draft.Expected : Array.Empty<string>();
        var keystone = RunOutcomeKeystone.Evaluate(
            PipelinePresets.ExpectsCodeChanges(pipelineName),
            PipelinePresets.ExpectsGreenTests(pipelineName),
            gitCommittedChange: anyCode,
            recordedChange: realCodeChanges > 0,
            verification,
            criteria);

        foreach (var (repo, sandbox, hasCode) in stagedRepos)
        {
            // Open a PR when this repo changed code, or — if nothing changed
            // anywhere — for the first repo as the run-record carrier.
            var isRecordCarrier = !anyCode && repo.Name == context.Configs[0].Name;
            if (!hasCode && !isRecordCarrier)
            {
                logger.LogInformation("{Repo}: no code changes — no PR (run record only)", repo.Name);
                opened.Add(new OpenedPullRequest(repo.Name, Url: null, OpenStatus.SkippedNoChanges));
                continue;
            }
            var (result, body) = await OpenOneAsync(
                context, sandbox, repo, isDraft: !keystone.Satisfied, cancellationToken);
            opened.Add(result);
            if (body is not null) bodies[repo.Name] = body;
        }

        context.Pipeline.Set<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests, opened);
        context.Pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.OpenedPullRequestBodies, bodies);
        var primaryUrl = opened.FirstOrDefault(o => o.Status == OpenStatus.Opened)?.Url;
        if (primaryUrl is not null)
            context.Pipeline.Set(ContextKeys.PullRequestUrl, primaryUrl);

        await PublishOutcomesAsync(context.Pipeline, opened, cancellationToken);

        // p0241 keystone: a fix/feature run that shipped no code, or whose
        // build/tests are not verified green, must NOT be reported as success and
        // must NOT mark the ticket resolved. The record PR (result.md) is already
        // opened above (as a draft when red), so the agent's reasoning is preserved
        // either way. Keystone was evaluated before the PR loop — reused here.
        if (!keystone.Satisfied)
        {
            // p0273: the work is NOT lost — OpenOneAsync already pushed the branch
            // and opened the PR(s) above, BEFORE this gate. Surface them so the
            // operator can review/take over a verification-red change, instead of a
            // "failed" step that reads as if nothing happened. The ticket stays
            // unfinalized (FinalizeTicketAsync is skipped) — correct for a red run.
            var openedUrls = opened
                .Where(o => o.Status == OpenStatus.Opened && o.Url is not null)
                .Select(o => o.Url!)
                .ToList();
            var prNote = openedUrls.Count > 0
                ? " The change is pushed and open for review (verification red): "
                  + string.Join(", ", openedUrls)
                : string.Empty;
            logger.LogWarning(
                "Keystone refused success for ticket {Ticket}: {Reason}{Pr}",
                context.Ticket.Id, keystone.FailureReason, prNote);
            return CommandResult.Fail($"{keystone.FailureReason}{prNote}");
        }

        await FinalizeTicketAsync(context, opened, cancellationToken);
        return BuildResult(opened, anyCode, context.Changes.Count);
    }

    // p0223: surface the structured per-repo outcome to the run detail so the UI
    // renders "no changes — no PR needed" / a clickable PR link / a real failure
    // reason, instead of the raw "git commit · exit 1" sandbox row.
    private async Task PublishOutcomesAsync(
        PipelineContext pipeline, IReadOnlyList<OpenedPullRequest> opened, CancellationToken ct)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return;
        foreach (var o in opened)
        {
            await events.PublishAsync(
                new PullRequestOutcomeEvent(runId!, o.RepoName, MapStatus(o.Status), DateTimeOffset.UtcNow, o.Url, o.Reason),
                ct);
        }
    }

    private static string MapStatus(OpenStatus status) => status switch
    {
        OpenStatus.Opened => "opened",
        OpenStatus.SkippedNoChanges => "no_changes",
        _ => "failed",
    };

    private static string Truncate(string message)
    {
        var line = message.Split('\n', 2)[0].Trim();
        return line.Length > 160 ? line[..160] : line;
    }

    private async Task<(OpenedPullRequest Result, string? Body)> OpenOneAsync(
        CommitAndPRContext context, ISandbox sandbox, RepoConnection repo, bool isDraft, CancellationToken ct)
    {
        var branch = context.Repository.CurrentBranch.Value;
        var message = $"fix: {context.Ticket.Title} (#{context.Ticket.Id})";
        try
        {
            await gitOps.StageAllAsync(sandbox, ct);
            // p0234: force-stage the run-record so EVERY repo commits + gets a
            // PR — WriteRunResult wrote .agentsmith/runs/{runId}/{plan,result}.md
            // (+ the agent's plan.md/decisions.md) into this repo, and a target
            // repo that .gitignores .agentsmith would otherwise have `git add -A`
            // silently skip it, leaving "nothing to commit" → no PR. The run
            // record must always be pushed.
            await gitOps.ForceStageAsync(sandbox, AgentSmithRunRecordPath, ct);
            // p0228: a repo with neither source changes NOR a run-record has
            // nothing to commit; skip the doomed `git commit` (which exits 1).
            // With the force-stage above this is now rare — but the agent dir
            // can legitimately be empty for a repo, so the guard stays.
            var stagedDiff = await gitOps.GetStagedDiffAsync(sandbox, ct);
            if (string.IsNullOrEmpty(stagedDiff))
            {
                // p0256: a spent run that opens no PR is a real loss. The run record
                // under .agentsmith was force-staged just above yet git sees nothing
                // staged — dump what git actually sees so the next real run pins the
                // root cause instead of this staying a silent skip.
                try
                {
                    var diag = await gitOps.DescribeRunRecordStateAsync(sandbox, ct);
                    logger.LogWarning(
                        "{Repo}: nothing staged after force-staging the run record — no PR. Diagnostics:\n{Diag}",
                        repo.Name, diag);
                }
                catch (Exception dex)
                {
                    logger.LogWarning(dex, "{Repo}: run-record stage diagnostic failed", repo.Name);
                }
                return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.SkippedNoChanges), null);
            }
            var leak = ScanDiff(repo.Name, stagedDiff);
            if (leak is not null)
            {
                logger.LogError("{Repo}: secret-pattern match in staged diff at {Where} — aborting commit", repo.Name, leak);
                return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed, $"secret-pattern match at {leak}"), null);
            }
            await gitOps.CommitAndPushStagedAsync(sandbox, branch, message, repo.Type, ct);
        }
        catch (Exception ex) when (LooksLikeEmptyCommit(ex))
        {
            logger.LogInformation("{Repo}: no changes, skipping PR", repo.Name);
            return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.SkippedNoChanges), null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Repo}: commit/push failed", repo.Name);
            return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed, Truncate(ex.Message)), null);
        }

        // A red run's PR is a draft and says so at the top of the body, so a reviewer
        // sees "verification red" before the ticket text — not a change that looks ready.
        var redBanner = isDraft
            ? "> ⚠️ **Verification red** — build/tests did not pass. Draft for review, do not merge as-is.\n\n"
            : string.Empty;
        // p0328: the ratified expectation renders as a reviewer checklist — the
        // PR's acceptance contract; headless runs show the 'unratified' stamp.
        var body = $"{redBanner}{context.Ticket.Description}"
            + $"{ExpectationPrBodySection.Build(context.Pipeline)}\n\n{SiblingMarker}";
        try
        {
            var provider = sourceFactory.Create(repo);
            var prUrl = await provider.CreatePullRequestAsync(
                new Repository(context.Repository.CurrentBranch, repo.Url ?? string.Empty),
                context.Ticket.Title, body, ct, linkedTicketId: context.Ticket.Id, isDraft: isDraft);
            logger.LogInformation("{Repo}: PR opened {Url}", repo.Name, prUrl);
            return (new OpenedPullRequest(repo.Name, prUrl, OpenStatus.Opened), body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Repo}: PR open failed", repo.Name);
            return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed, Truncate(ex.Message)), null);
        }
    }

    private static bool LooksLikeEmptyCommit(Exception ex) =>
        ex.Message.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("no changes", StringComparison.OrdinalIgnoreCase);

    // p0192: defence-in-depth around the master-prompt rule from p0191. The
    // agent is instructed to apply credentials at user-config level
    // (~/.nuget/...), never to the repo's own files — this scan is the
    // gate that runs anyway, in case the rule is ignored. First match wins;
    // the operator sees the file:line in the failure log.
    private string? ScanDiff(string repoName, string diff)
    {
        var matches = secretScanner.Scan($"{repoName}-staged-diff", diff);
        return matches.Count == 0 ? null : $"line {matches[0].Line} ({matches[0].Pattern})";
    }

    private Task FinalizeTicketAsync(
        CommitAndPRContext context, IReadOnlyList<OpenedPullRequest> opened, CancellationToken ct)
    {
        if (!opened.Any(o => o.Status == OpenStatus.Opened)) return Task.CompletedTask;

        // p0326: an inline ticket exists only on this run — there is no tracker
        // item to comment on or transition, so skip instead of a doomed provider call.
        if (context.Pipeline.Has(ContextKeys.InlineTicket)) return Task.CompletedTask;

        var changes = string.Join("\n",
            context.Changes.Select(c => $"- [{c.ChangeType}] `{c.Path}`"));
        var summary = $"""
            ## Agent Smith - Completed across {context.Configs.Count} repo(s)

            ### Pull requests
            {RenderPullRequestList(opened)}

            ### Changes
            {changes}

            This ticket was automatically processed by Agent Smith.
            """;
        context.Pipeline.TryGet<string>(ContextKeys.DoneStatus, out var doneStatus);
        return TicketLifecycle.FinalizeAsync(
            ticketFactory, context.TrackerConnection, context.Ticket.Id,
            doneStatus, summary, logger, ct);
    }

    private static string RenderPullRequestList(IReadOnlyList<OpenedPullRequest> opened) =>
        string.Join("\n", opened.Select(o => o.Status switch
        {
            OpenStatus.Opened => $"- **{o.RepoName}**: {o.Url}",
            OpenStatus.SkippedNoChanges => $"- **{o.RepoName}**: _(no changes)_",
            _ => $"- **{o.RepoName}**: _(open failed)_",
        }));

    // p0235: a clear, factual run outcome — this message becomes the step's
    // result line, so it must say plainly what happened (changes + PR, or "no
    // code changes"), not a bare URL.
    private static CommandResult BuildResult(
        IReadOnlyList<OpenedPullRequest> opened, bool anyCode, int changeCount)
    {
        var openedEntries = opened.Where(o => o.Status == OpenStatus.Opened).ToList();
        var failed = opened.Count(o => o.Status == OpenStatus.Failed);
        if (openedEntries.Count == 0 && failed > 0)
            return CommandResult.Fail($"All {opened.Count} PR open attempts failed.");
        if (openedEntries.Count == 0)
            return CommandResult.Ok("No PR opened (nothing to record).");
        var urls = string.Join(", ", openedEntries.Select(o => o.Url));
        if (!anyCode)
            return CommandResult.Ok(
                $"No code changes were applied — run recorded in PR: {urls} (safe to close).");
        var prWord = openedEntries.Count == 1 ? "PR" : "PRs";
        return CommandResult.Ok($"Completed: {changeCount} file(s) changed — {prWord}: {urls}");
    }
}
