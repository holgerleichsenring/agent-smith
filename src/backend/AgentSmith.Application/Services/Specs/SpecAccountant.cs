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
    SpecAccountCall call,
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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (criteria.Count == 0)
            return new SpecAccount(repoKey, [], "the phase states no completion criteria");

        var resolver = new CitationResolver(CitedFileIndex.FromDiff(diff), commandResults);
        // p0482: the account settles an absence by searching the branch itself, so the call
        // carries a tool and an iteration cap. Without a sandbox it falls back to the cited
        // evidence, which is what every account did before this.
        var searchable = branchSearch?.Repositories;
        var tools = AccountTools.For(branchSearch);
        var chat = chatClientFactory.Create(
            agent, TaskType.Reasoning, tools is null ? null : AccountTools.MaxIterations);

        // A diff too large for one call is SPLIT, never cut: evidence is monotone, so a
        // criterion satisfied by one window is satisfied, and the windows' answers union.
        var windows = DiffWindows.Split(diff);
        if (windows.Count > 1)
            logger.LogInformation(
                "{Repo}: the delivery diff spans {Windows} windows — accounting for each and "
                + "taking a criterion as satisfied where any window shows it",
                repoKey, windows.Count);

        var answer = await AskEveryWindowAsync(
            chat, repoKey, criteria, windows, commandResults, searchable, tools,
            costTracker, cancellationToken);
        if (answer is null)
            return new SpecAccount(repoKey, [], "the accounting call returned nothing readable");

        var reader = new AccountRowResolution(logger);
        var rows = reader.Resolve(repoKey, criteria, answer, resolver);

        // p0474: a citation that resolves against nothing is a FORMAT failure far more often
        // than a false claim — three live runs died on it with the work finished. The deriver
        // has been given its objection and a second attempt since p0422; the account gets one
        // too, and the resolver judges the second answer exactly as it judged the first.
        var unresolved = AccountReAsk.Unresolved(rows);
        if (unresolved.Count == 0) return new SpecAccount(repoKey, rows);

        logger.LogInformation(
            "{Repo}: {Count} criterion(s) cited something that resolves against nothing — asking once more",
            repoKey, unresolved.Count);
        var second = await call.AskAsync(
            chat, repoKey, [.. unresolved.Select(u => u.Criterion)], windows.Count > 0 ? windows[0] : string.Empty,
            searchable, commandResults, costTracker, cancellationToken,
            AccountReAsk.Message(unresolved), tools);
        if (second is null) return new SpecAccount(repoKey, rows);

        var corrected = reader.Resolve(repoKey, [.. unresolved.Select(u => u.Criterion)], second, resolver);
        return new SpecAccount(repoKey, [.. rows.Select(r =>
            corrected.FirstOrDefault(c =>
                string.Equals(c.Criterion, r.Criterion, StringComparison.OrdinalIgnoreCase)) is { Satisfied: true } fixedRow
                ? fixedRow : r)]);
    }

    private const string RoleName = "spec-accountant";

    /// <summary>
    /// Every window is asked; <see cref="AccountWindowMerge"/> decides what their answers
    /// mean together. A window that could not see the evidence is a statement about that
    /// slice, never about the branch.
    /// </summary>
    private async Task<IReadOnlyList<AccountRow>?> AskEveryWindowAsync(
        IChatClient chat, string repoKey, IReadOnlyList<string> criteria,
        IReadOnlyList<string> windows, IReadOnlyList<string> commandResults,
        IReadOnlyList<string>? searchable, IList<AITool>? tools,
        PipelineCostTracker costTracker, CancellationToken ct)
    {
        var answers = new List<IReadOnlyList<AccountRow>>();
        foreach (var window in windows.Count == 0 ? [string.Empty] : windows)
        {
            var rows = await call.AskAsync(
                chat, repoKey, criteria, window, searchable, commandResults, costTracker, ct, tools: tools);
            if (rows is not null) answers.Add(rows);
        }
        return answers.Count == 0 ? null : AccountWindowMerge.Of(answers);
    }
}
