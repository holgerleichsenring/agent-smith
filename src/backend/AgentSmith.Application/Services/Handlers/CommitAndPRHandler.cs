using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
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
            // p0360: work already committed by mid-run checkpoints leaves a clean
            // tree here — the checkpoint record keeps the repo counting as changed.
            var hasCode = staged.Any(n => !RunRecordPaths.IsRunRecordPath(n))
                || RunWorkCheckpointer.HasCheckpointedCode(context.Pipeline, repo.Name);
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
        // p0393a: the acceptance contract is the current phase's done-list, falling back to
        // a ratified expectation for pipelines that still negotiate one.
        var criteria = Specs.AcceptanceCriteria.For(context.Pipeline);
        // p0341c: the ledger + the run's changed paths let the keystone downgrade a
        // truncated run that self-reported Met over still-open steps. Empty ledger /
        // no-contract runs are unchanged (the cross-check falls through to p0340).
        var ledger = context.Pipeline.TryGet<Contracts.Progress.ProgressLedger>(
            ContextKeys.ProgressLedger, out var lg) ? lg : null;
        var changedPaths = context.Changes.Select(c => c.Path.ToString()).ToList();
        // p0384: per-repo staged truth + the classifier's must-change subset let the
        // keystone fail a run that skipped an expected-change repo, naming the repo.
        // Absent expected set => anyCode semantics, unchanged.
        var perRepoCode = stagedRepos.ToDictionary(
            s => s.Repo.Name ?? string.Empty, s => s.HasCode, StringComparer.OrdinalIgnoreCase);
        var expectedChangeRepos = context.Pipeline.TryGet<IReadOnlyList<string>>(
            ContextKeys.ExpectedChangeRepos, out var ecr) ? ecr : null;
        var keystone = RunOutcomeKeystone.Evaluate(
            PipelinePresets.ExpectsCodeChanges(pipelineName),
            PipelinePresets.ExpectsGreenTests(pipelineName),
            gitCommittedChange: anyCode,
            recordedChange: realCodeChanges > 0,
            verification,
            criteria,
            ledger,
            changedPaths,
            perRepoCommittedChange: perRepoCode,
            expectedChangeRepos: expectedChangeRepos,
            // p0400: a ratified ships_code:false phase is judged by its done criteria —
            // the no-diff rules stand down; the per-repo expected-changes gate does not.
            shipsCode: Specs.PhaseDelivery.ShipsCode(context.Pipeline));

        // p0393a: a sequence that stopped mid-way leaves a HALF-MIGRATED repository — some
        // phases applied, others not. That state must be unmergeable BY CONSTRUCTION, not
        // merely accompanied by a red check somebody can override, because it is the one
        // failure that looks finished.
        var progress = context.Pipeline.TryGet<SpecSequenceProgress>(
            ContextKeys.SpecSequenceProgress, out var seq) ? seq : null;
        var halfMigrated = progress?.IsPartial == true;

        foreach (var (repo, sandbox, hasCode) in stagedRepos)
        {
            // Open a PR when this repo changed code, or — if nothing changed
            // anywhere — for the first repo as the run-record carrier.
            var isRecordCarrier = !anyCode && repo.Name == context.Configs[0].Name;
            OpenedPullRequest outcome;
            if (!hasCode && !isRecordCarrier)
            {
                logger.LogInformation("{Repo}: no code changes — no PR (run record only)", repo.Name);
                outcome = new OpenedPullRequest(repo.Name, Url: null, OpenStatus.SkippedNoChanges);
            }
            else
            {
                var (result, body) = await OpenOneAsync(
                    context, sandbox, repo,
                    isDraft: !keystone.Satisfied || halfMigrated, progress, cancellationToken);
                outcome = result;
                if (body is not null) bodies[repo.Name] = body;
            }
            opened.Add(outcome);
            // p0350: record each PR the MOMENT it is decided, not batched after the
            // whole loop. A PR is a committed Azure DevOps side-effect; opening it
            // and recording it were not atomic, so a later interruption (a
            // token-limit throw at a subsequent step) left the PRs live in Azure
            // DevOps but "No PR opened" on the run. Publishing per repo means every
            // already-opened PR survives on the run regardless of what fails next.
            await PublishOutcomeAsync(context.Pipeline, outcome, cancellationToken);
        }

        context.Pipeline.Set<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests, opened);
        context.Pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.OpenedPullRequestBodies, bodies);
        var primaryUrl = opened.FirstOrDefault(o => o.Status == OpenStatus.Opened)?.Url;
        if (primaryUrl is not null)
            context.Pipeline.Set(ContextKeys.PullRequestUrl, primaryUrl);

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
    // p0350: one repo at a time, emitted inline in the PR loop the moment each
    // outcome is known — see the loop comment for why batching lost PRs.
    private async Task PublishOutcomeAsync(
        PipelineContext pipeline, OpenedPullRequest o, CancellationToken ct)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return;
        await events.PublishAsync(
            new PullRequestOutcomeEvent(runId!, o.RepoName, MapStatus(o.Status), DateTimeOffset.UtcNow, o.Url, o.Reason),
            ct);
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
        CommitAndPRContext context, ISandbox sandbox, RepoConnection repo, bool isDraft,
        SpecSequenceProgress? progress, CancellationToken ct)
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
                if (!RunWorkCheckpointer.WasCheckpointed(context.Pipeline, repo.Name))
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
                // p0360: mid-run checkpoints already committed (and scanned) the work —
                // a clean tree here is delivery, not emptiness. Ensure the remote has
                // HEAD and open the PR over the checkpoint commits.
                await gitOps.PushHeadAsync(sandbox, branch, repo.Type, ct);
            }
            else
            {
                var leak = ScanDiff(repo.Name, stagedDiff);
                if (leak is not null)
                {
                    logger.LogError("{Repo}: secret-pattern match in staged diff at {Where} — aborting commit", repo.Name, leak);
                    return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed, $"secret-pattern match at {leak}"), null);
                }
                await gitOps.CommitAndPushStagedAsync(sandbox, branch, message, repo.Type, ct);
            }
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
        // p0328/p0393a: the acceptance contract renders as a reviewer checklist. p0393a
        // adds the per-phase table and the discarded list — for a stopped sequence the
        // table is the statement that the repository is half migrated, spelled out phase
        // by phase instead of implied by a red check.
        var body = $"{redBanner}{context.Ticket.Description}"
            + $"{ExpectationPrBodySection.Build(context.Pipeline)}"
            + $"{SpecPrBodySection.Build(context.Pipeline, progress)}\n\n{SiblingMarker}";
        try
        {
            var provider = sourceFactory.Create(repo);
            var branchRepo = new Repository(context.Repository.CurrentBranch, repo.Url ?? string.Empty);
            // p0390: FIND-or-create. A run now opens its draft PR early, at the work-spec
            // commit, so a reviewer has something to edit while the run is still working.
            // An unconditional second create on the same branch throws into the catch
            // below and reports the whole PR step Failed; reusing it and refreshing the
            // body carries the run's outcome onto the PR that already exists.
            var existing = await provider.FindOpenPullRequestAsync(branchRepo, ct);
            if (existing is not null)
                return (await RefreshAsync(provider, repo.Name, existing, body, isDraft, ct), body);
            var prUrl = await provider.CreatePullRequestAsync(
                branchRepo, context.Ticket.Title, body, ct,
                linkedTicketId: context.Ticket.Id, isDraft: isDraft);
            logger.LogInformation("{Repo}: PR opened {Url}", repo.Name, prUrl);
            return (new OpenedPullRequest(repo.Name, prUrl, OpenStatus.Opened), body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Repo}: PR open failed", repo.Name);
            return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed, Truncate(ex.Message)), null);
        }
    }

    // p0390: the PR already exists (opened at the work-spec commit, or by an earlier
    // run on this ticket branch). Refresh its body so the run's outcome — the red
    // banner, the acceptance checklist, the sibling marker — reaches the reviewer.
    // A failed body update is NOT a failed PR: the PR is open and the work is on it.
    private async Task<OpenedPullRequest> RefreshAsync(
        ISourceProvider provider, string repoName, string prUrl, string body, bool isDraft,
        CancellationToken ct)
    {
        var updated = await provider.UpdatePullRequestBodyAsync(prUrl, body, ct);
        // p0393a: the PR was opened as a DRAFT at the spec commit, before any phase was
        // verified. Only a complete, green run takes it out of draft — a stopped sequence
        // stays unmergeable by construction, which is the whole point of opening it early.
        var ready = !isDraft && await provider.MarkPullRequestReadyAsync(prUrl, ct);
        logger.LogInformation(
            "{Repo}: reusing the PR opened earlier on this branch {Url} "
            + "(body updated: {Updated}, ready for review: {Ready})",
            repoName, prUrl, updated, ready);
        return new OpenedPullRequest(repoName, prUrl, OpenStatus.Opened);
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
