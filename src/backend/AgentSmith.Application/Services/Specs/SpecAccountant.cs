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
/// "satisfied" carries a citation that <see cref="DiffFileIndex"/> resolves against the
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
        PipelineCostTracker costTracker,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (criteria.Count == 0)
            return new SpecAccount(repoKey, [], "the phase states no completion criteria");

        var resolver = new CitationResolver(new DiffFileIndex(diff), commandResults);
        var chat = chatClientFactory.Create(agent, TaskType.Reasoning);

        // A diff too large for one call is SPLIT, never cut: evidence is monotone, so a
        // criterion satisfied by one window is satisfied, and the windows' answers union.
        var windows = DiffWindows.Split(diff);
        if (windows.Count > 1)
            logger.LogInformation(
                "{Repo}: the delivery diff spans {Windows} windows — accounting for each and "
                + "taking a criterion as satisfied where any window shows it",
                repoKey, windows.Count);

        var answer = await AskEveryWindowAsync(
            chat, repoKey, criteria, windows, commandResults, costTracker, cancellationToken);
        if (answer is null)
            return new SpecAccount(repoKey, [], "the accounting call returned nothing readable");

        var rows = new List<CriterionAccount>();
        foreach (var criterion in criteria)
        {
            var row = answer.FirstOrDefault(r =>
                string.Equals(r.Criterion, criterion, StringComparison.OrdinalIgnoreCase))
                ?? new AccountRow(criterion, false, null, "the account did not address this criterion");
            rows.Add(Report(repoKey, resolver.Resolve(row)));
        }

        return new SpecAccount(repoKey, rows);
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
        PipelineCostTracker costTracker, CancellationToken ct)
    {
        var answers = new List<IReadOnlyList<AccountRow>>();
        foreach (var window in windows.Count == 0 ? [string.Empty] : windows)
        {
            var rows = await call.AskAsync(chat, repoKey, criteria, window, commandResults, costTracker, ct);
            if (rows is not null) answers.Add(rows);
        }
        return answers.Count == 0 ? null : AccountWindowMerge.Of(answers);
    }

    private CriterionAccount Report(string repoKey, CriterionAccount resolved)
    {
        if (!resolved.Satisfied && resolved.Note?.Contains("neither", StringComparison.Ordinal) == true)
            logger.LogWarning(
                "{Repo}: {Criterion} — {Note}", repoKey, Shorten(resolved.Criterion), resolved.Note);
        return resolved;
    }

    private static string Shorten(string text) =>
        text.Length <= 60 ? text : text[..60] + "…";

}
