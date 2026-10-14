using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
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
    PullRequestOutcomePublisher outcomes,
    TicketLifecycle ticketLifecycle,
    SandboxTargets sandboxTargets,
    Specs.SpecDeliveryAccounting accounting,
    FailedRunPersistence failedRun,
    SpecOnlyPullRequests specOnlyPrs,
    ShortfallDelivery shortfallDelivery,
    CompletedRunTicketSummary completedSummary,
    Structure.StructureDivergencePublisher divergences,
    CodePullRequestBanner banners,
    SeriesChunkStop chunkStop,
    ILogger<CommitAndPRHandler> logger)
    : ICommandHandler<CommitAndPRContext>
{
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
        var stagedRepos = new List<(RepoConnection Repo, string Key, ISandbox Sandbox, bool HasCode)>();
        var opened = new List<OpenedPullRequest>(context.Configs.Count);
        var bodies = new Dictionary<string, string>(context.Configs.Count, StringComparer.Ordinal);
        foreach (var repo in context.Configs)
        {
            var matches = sandboxTargets.SandboxesForRepo(context.Pipeline, repo);
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
            stagedRepos.Add((repo, matches[0].Key, sandbox, hasCode));
        }

        var anyCode = stagedRepos.Any(s => s.HasCode);
        // 2026-10-12-fbbda: divergences for the PR bodies. 2026-10-13-2d0c: only a run with code has any.
        if (anyCode) await divergences.PublishAsync(context.Pipeline, cancellationToken);

        // 2026-09-07-f420: the p0237 finalizer tail reaches this handler after a step has
        // FAILED. The verdict is then already known, so the account is not taken and the
        // keystone is not asked; the PR is a draft that names the failure, and the ticket
        // is left to the error path — the tail used to finalize it "Completed" with the
        // done status before the failure comment landed (runs b7bc and 0c88).
        // p0439: unless the run stopped AFTER a verified spec — then that spec is
        // delivered (its verification was the keystone) and the rest is named as missing.
        var failureReason = failedRun.Reason(context.Pipeline);
        var shortfall = failureReason is null ? null : await shortfallDelivery.PrepareAsync(
            context.Pipeline, [.. stagedRepos.Select(s => (s.Key, s.Sandbox))], cancellationToken);
        var keystone = failureReason is null ? await EvaluateKeystoneAsync(context, cancellationToken)
            : shortfall is not null ? DeliveryVerdict.Ok() : DeliveryVerdict.Fail(failureReason);

        // p0393a: a sequence that stopped mid-way leaves a HALF-MIGRATED repository — some
        // specs applied, others not. That state must be unmergeable BY CONSTRUCTION, not
        // merely accompanied by a red check somebody can override, because it is the one
        // failure that looks finished. A delivered shortfall carries only verified specs.
        var progress = context.Pipeline.TryGet<SpecSequenceProgress>(
            ContextKeys.SpecSequenceProgress, out var seq) ? seq : null;
        var halfMigrated = progress?.IsPartial == true && shortfall is null;
        // 2026-10-12-3a36c: failed means NOT delivered — a delivered shortfall stays a delivery.
        var failed = failureReason is not null && shortfall is null;
        var isDraft = !keystone.Satisfied || halfMigrated;

        foreach (var (repo, _, sandbox, hasCode) in stagedRepos)
        {
            // Open a PR when this repo changed code, or — if nothing changed
            // anywhere — for the first repo as the run-record carrier.
            // 2026-10-12-3a36c: never on a failed run; it commits only work it did.
            var isRecordCarrier = !anyCode && !failed && repo.Name == context.Configs[0].Name;
            OpenedPullRequest outcome;
            if (!hasCode && !isRecordCarrier)
            {
                logger.LogInformation("{Repo}: no code changes — no PR (run record only)", repo.Name);
                outcome = new OpenedPullRequest(repo.Name, Url: null, OpenStatus.SkippedNoChanges);
            }
            else
            {
                var (result, body) = await OpenOneAsync(
                    context, sandbox, repo, isDraft,
                    failureReason, shortfall, progress, cancellationToken);
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
            await outcomes.PublishAsync(context.Pipeline, outcome, cancellationToken);
        }

        await KeepSpecPullRequestsAsync(
            context, opened, bodies, banners.For(failureReason, shortfall, isDraft, progress), !isDraft && !failed, progress, shortfall, cancellationToken);

        context.Pipeline.Set<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests, opened);
        context.Pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.OpenedPullRequestBodies, bodies);
        var primaryUrl = opened.FirstOrDefault(o => o.Status == OpenStatus.Opened)?.Url;
        if (primaryUrl is not null)
            context.Pipeline.Set(ContextKeys.PullRequestUrl, primaryUrl);

        // p0439: a shortfall that reached a pull request is delivered — the ticket gets the
        // done status and the delivery summary, and the run reads the delivery off the
        // context. One that opened no PR is a failed run like any other.
        if (shortfall is not null && primaryUrl is not null)
        {
            await FinalizeTicketAsync(context, opened, shortfall, cancellationToken);
            shortfall.MarkDelivered(context.Pipeline);
            return shortfallDelivery.StepResult(shortfall, opened);
        }
        // 2026-09-07-f420: a failed run's work is persisted above; nothing below — the
        // keystone's wording, the summary, the done status — describes it. The failed
        // step is the run's report; the error path posts it with what was kept.
        if (failureReason is not null)
            return failedRun.StepResult(failureReason, opened);
        // 2026-10-13-8762u: an attempt stopped at a series boundary keeps its draft, says where the series
        // continues, and never finalizes — the ticket keeps its status and its approval for the next attempt.
        if (progress?.StoppedAtChunk == true)
            return await chunkStop.ReportAsync(context, progress, cancellationToken);

        // p0241 keystone: a fix/feature run that shipped no code, or whose
        // build/tests are not verified green, must NOT be reported as success and
        // must NOT mark the ticket resolved. The record PR (result.md) is already
        // opened above (as a draft when red), so the agent's reasoning is preserved
        // either way. Keystone was evaluated before the PR loop — reused here.
        if (!keystone.Satisfied)
        {
            // p0273: the work is NOT lost — the PR(s) are open above, before this gate.
            // Surface them; the ticket stays unfinalized, correct for a red run.
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

        await FinalizeTicketAsync(context, opened, shortfall: null, cancellationToken);
        return BuildResult(opened, anyCode, context.Changes.Count);
    }

    // p0300c: the outcome keystone is evaluated BEFORE opening PRs so a verification-red
    // run opens its PR(s) as DRAFT (visible for review, not mergeable) instead of a
    // normal PR that reads as a green, ready change. Same inputs the post-loop gate
    // uses — hoisted, not duplicated.
    private async Task<DeliveryVerdict> EvaluateKeystoneAsync(
        CommitAndPRContext context, CancellationToken ct)
    {
        // p0393a: the acceptance contract is the current spec's done-list, falling back to
        // a ratified expectation for pipelines that still negotiate one.
        var criteria = Specs.AcceptanceCriteria.For(context.Pipeline);
        // p0421: ONE gate, reading what every spec accounted for against the branch.
        // Its predecessor asked whether THIS run had committed code and needed six
        // signals to teach that question its exceptions — a resumed branch carrying a
        // complete delivery still came out FAILED.
        var accounts = await accounting.TakeOrReuseAsync(context.Pipeline, criteria, ct);
        return RunDeliveryGate.Evaluate(accounts, criteria.Count);
    }

    // 2026-10-12-3a36c, generalised by 2026-10-12-f5f7e: every series repository without code keeps
    // the spec PR opened at derivation as an 'opened' outcome with the run's body, ready only when
    // the run is — on green and failed runs alike.
    private async Task KeepSpecPullRequestsAsync(
        CommitAndPRContext context, List<OpenedPullRequest> opened, Dictionary<string, string> bodies, string banner,
        bool ready, SpecSequenceProgress? progress, RunShortfall? shortfall, CancellationToken ct)
    {
        var body = CodePullRequestBody.Compose(context, banner, progress, shortfall);
        foreach (var kept in await specOnlyPrs.KeepAsync(context.Pipeline, context.Configs, opened, body, ready, ct))
            bodies[kept.RepoName] = body;
    }

    private async Task<(OpenedPullRequest Result, string? Body)> OpenOneAsync(
        CommitAndPRContext context, ISandbox sandbox, RepoConnection repo, bool isDraft,
        string? failureReason, RunShortfall? shortfall, SpecSequenceProgress? progress, CancellationToken ct)
    {
        var branch = context.Repository.CurrentBranch.Value;
        var message = failureReason is not null && shortfall is null
            ? failedRun.CommitMessage(context.Pipeline)
            : $"fix: {context.Ticket.Title} (#{context.Ticket.Id})";
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
                await gitOps.PushHeadAsync(sandbox, branch, repo, ct);
            }
            else
            {
                var leak = ScanDiff(repo.Name, stagedDiff);
                if (leak is not null)
                {
                    logger.LogError("{Repo}: secret-pattern match in staged diff at {Where} — aborting commit", repo.Name, leak);
                    return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed, $"secret-pattern match at {leak}"), null);
                }
                await gitOps.CommitAndPushStagedAsync(sandbox, branch, message, repo, ct);
            }
        }
        catch (Exception ex) when (EmptyCommit.Explains(ex))
        {
            logger.LogInformation("{Repo}: no changes, skipping PR", repo.Name);
            return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.SkippedNoChanges), null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Repo}: commit/push failed", repo.Name);
            return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed, PullRequestOutcomeText.FirstLine(ex.Message)), null);
        }

        var body = CodePullRequestBody.Compose(context, banners.For(failureReason, shortfall, isDraft, progress), progress, shortfall, repo.Name);
        try
        {
            var provider = sourceFactory.Create(repo);
            var branchRepo = new Repository(context.Repository.CurrentBranch, repo.Url ?? string.Empty);
            // p0390: FIND-or-create. A run now opens its draft PR early, at the work-spec
            // commit, so a reviewer has something to edit while the run is still working.
            // An unconditional second create on the same branch throws into the catch
            // below and reports the whole PR step Failed; reusing it and refreshing the
            // body carries the run's outcome onto the PR that already exists.
            // 2026-09-13-a284: per repository, because the rung is — this loop carries one
            // branch value for every repo, so one target would send the second one wrong.
            var target = PullRequestTargets.For(context.Pipeline, repo.Name);
            var existing = await provider.FindOpenPullRequestAsync(branchRepo, ct);
            if (existing is not null)
                return (await RefreshAsync(provider, repo.Name, existing, body, isDraft, target, ct), body);
            var prUrl = await provider.CreatePullRequestAsync(
                branchRepo, context.Ticket.Title, body, ct,
                linkedTicketId: context.Ticket.Id, isDraft: isDraft, targetBranch: target);
            logger.LogInformation("{Repo}: PR opened {Url}", repo.Name, prUrl);
            return (new OpenedPullRequest(repo.Name, prUrl, OpenStatus.Opened), body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Repo}: PR open failed", repo.Name);
            return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed, PullRequestOutcomeText.FirstLine(ex.Message)), null);
        }
    }

    // p0390: the PR already exists (opened at the work-spec commit, or by an earlier
    // run on this ticket branch). Refresh its body so the run's outcome — the red
    // banner, the acceptance checklist, the sibling marker — reaches the reviewer.
    // A failed body update is NOT a failed PR: the PR is open and the work is on it.
    private async Task<OpenedPullRequest> RefreshAsync(
        ISourceProvider provider, string repoName, string prUrl, string body, bool isDraft,
        BranchName? target, CancellationToken ct)
    {
        // 2026-09-13-a284: this pull request was opened at the SPEC commit, against the
        // provider's default branch and possibly before the rung existed — and until now
        // nothing anywhere changed a base. Left on it, a slice's review shows every
        // predecessor slice's work as well as its own.
        if (target is not null
            && PullRequestTargets.NeedsMove(await provider.ReadPullRequestBaseAsync(prUrl, ct), target))
        {
            var moved = await provider.RetargetPullRequestAsync(prUrl, target, ct);
            logger.LogInformation(
                "{Repo}: the pull request belongs on '{Target}' (moved: {Moved})",
                repoName, target.Value, moved);
        }
        var updated = await provider.UpdatePullRequestBodyAsync(prUrl, body, ct);
        // p0393a: the PR was opened as a DRAFT at the spec commit, before any spec was
        // verified. Only a complete, green run takes it out of draft — a stopped sequence
        // stays unmergeable by construction, which is the whole point of opening it early.
        var ready = !isDraft && await provider.MarkPullRequestReadyAsync(prUrl, ct);
        logger.LogInformation(
            "{Repo}: reusing the PR opened earlier on this branch {Url} "
            + "(body updated: {Updated}, ready for review: {Ready})",
            repoName, prUrl, updated, ready);
        return new OpenedPullRequest(repoName, prUrl, OpenStatus.Opened);
    }

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
        CommitAndPRContext context, IReadOnlyList<OpenedPullRequest> opened, RunShortfall? shortfall,
        CancellationToken ct)
    {
        if (!opened.Any(o => o.Status == OpenStatus.Opened)) return Task.CompletedTask;

        // p0326: an inline ticket exists only on this run — there is no tracker
        // item to comment on or transition, so skip instead of a doomed provider call.
        if (context.Pipeline.Has(ContextKeys.InlineTicket)) return Task.CompletedTask;

        var summary = completedSummary.Build(
            context.Pipeline, context.Configs.Count, opened, context.Changes, shortfall);
        context.Pipeline.TryGet<string>(ContextKeys.DoneStatus, out var doneStatus);
        return ticketLifecycle.FinalizeAsync(
            ticketFactory, context.TrackerConnection, context.Ticket.Id,
            doneStatus, summary, logger, ct);
    }

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
