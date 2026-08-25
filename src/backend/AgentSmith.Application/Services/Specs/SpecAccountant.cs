using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0420: asks a FRESH model instance what the branch diff does not satisfy.
/// <para>
/// Three things keep this from being a rubber stamp. It reads the DIFF, never the
/// working history, so it has no reasoning of its own to defend. It is asked
/// adversarially — "what is missing" — because "all done" is the cheap answer to the
/// positive question and the expensive answer to the negative one. And every
/// "satisfied" carries a citation that <see cref="CitedFileIndex"/> resolves against the
/// diff, so a criterion cannot be satisfied by a file the phase never touched.
/// </para>
/// <para>
/// What stays unverified: a real path may fail to mean what the account claims. No
/// affordable check closes that; the account exists to make the claim refutable in
/// twenty seconds, not to prove it.
/// </para>
/// </summary>
public sealed class SpecAccountant(
    IChatClientFactory chatClientFactory,
    AccountCalls calls,
    ILogger<SpecAccountant> logger) : ISpecAccountant
{
    public async Task<SpecAccount> AccountAsync(
        string repoKey,
        IReadOnlyList<string> criteria,
        string diff,
        IReadOnlyList<string> commandResults,
        AgentConfig agent,
        BranchSearch? branchSearch,
        PipelineCostTracker costTracker,
        CancellationToken cancellationToken,
        int windowBudgetChars = DiffWindows.DefaultBudgetChars)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (criteria.Count == 0)
            return new SpecAccount(repoKey, [], "the phase states no completion criteria");

        // p0483: the account settles an absence by searching the branch itself, so the call
        // carries a tool and an iteration cap. Without a sandbox it falls back to the cited
        // evidence, which is what every account did before this.
        var searchable = branchSearch?.Repositories;
        var tools = AccountTools.For(branchSearch);
        var chat = chatClientFactory.Create(
            agent, TaskType.Reasoning, tools is null ? null : AccountTools.MaxIterations);

        // A diff too large for one call is SPLIT, never cut: evidence is monotone, so a
        // criterion satisfied by one window is satisfied, and the windows' answers union.
        // 2026-08-25-1360: derived ONCE, from the whole delivery, and handed to every call.
        // A window's own files are not the branch's files, and the heading says complete.
        var deliveryFiles = CitedFileIndex.FromDiff(diff);
        var split = DiffWindows.Split(diff, windowBudgetChars);
        if (split.Count > 1)
            logger.LogInformation(
                "{Repo}: the delivery diff spans {Windows} windows — accounting for each and "
                + "taking a criterion as satisfied where any window shows it",
                repoKey, split.Count);

        var answer = await calls.AskEveryAsync(
            chat, repoKey, criteria, split, commandResults, searchable, tools,
            deliveryFiles, costTracker, cancellationToken);
        if (answer is null)
            return new SpecAccount(repoKey, [], "the accounting call returned nothing readable");

        // p0484: built AFTER the call. The account's own searches happen DURING it, so a
        // resolver made ahead of time can never see them — which is why the first live run
        // was refused for a criterion it had settled by looking.
        var reader = new AccountRowResolution(logger);
        var rows = reader.Resolve(repoKey, criteria, answer,
            AccountTools.ResolverOver(diff, commandResults, branchSearch));

        var unresolved = AccountReAsk.Unresolved(rows);
        if (unresolved.Count == 0) return new SpecAccount(repoKey, rows);
        logger.LogInformation(
            "{Repo}: {Count} criterion(s) cited something that resolves against nothing — asking once more",
            repoKey, unresolved.Count);
        // The correction demands a path copied exactly as the FILE LIST prints it, so it
        // needs the same complete list. Shown windows[0]'s list, a criterion whose file
        // lives in a later window was being asked to comply with a list that cannot hold it.
        var second = await calls.AskCorrectionAsync(
            chat, repoKey, [.. unresolved.Select(u => u.Criterion)],
            split.Count > 0 ? split[0] : string.Empty,
            searchable, commandResults, tools, deliveryFiles,
            AccountReAsk.Message(unresolved), costTracker, cancellationToken);
        return new SpecAccount(repoKey, AccountSecondPass.Merge(
            rows, unresolved, second, repoKey, reader,
            AccountTools.ResolverOver(diff, commandResults, branchSearch)));
    }

    private const string RoleName = "spec-accountant";

}
