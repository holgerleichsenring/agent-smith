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
/// What stays unverified, stated plainly: a real path may fail to mean what the account
/// claims. No affordable check closes that, and pretending otherwise is what the old
/// gate did. The account exists to make the claim refutable in twenty seconds.
/// </para>
/// </summary>
public sealed class SpecAccountant(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext,
    ILogger<SpecAccountant> logger)
{
    public async Task<SpecAccount> AccountAsync(
        string repoKey,
        IReadOnlyList<string> criteria,
        string diff,
        AgentConfig agent,
        PipelineCostTracker costTracker,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (criteria.Count == 0)
            return new SpecAccount(repoKey, [], "the phase states no completion criteria");

        var index = new DiffFileIndex(diff);
        var chat = chatClientFactory.Create(agent, TaskType.Reasoning);
        var answer = await AskAsync(chat, repoKey, criteria, diff, costTracker, cancellationToken);
        if (answer is null)
            return new SpecAccount(repoKey, [], "the accounting call returned nothing readable");

        var rows = new List<CriterionAccount>();
        foreach (var criterion in criteria)
        {
            var row = answer.FirstOrDefault(r =>
                string.Equals(r.Criterion, criterion, StringComparison.OrdinalIgnoreCase))
                ?? new AccountRow(criterion, false, null, "the account did not address this criterion");
            rows.Add(Resolve(repoKey, row, index));
        }

        return new SpecAccount(repoKey, rows);
    }

    /// <summary>
    /// A citation that names nothing in the diff turns its criterion into NOT satisfied,
    /// and says so — the account is wrong about the world, not merely imprecise.
    /// </summary>
    private CriterionAccount Resolve(string repoKey, AccountRow row, DiffFileIndex index)
    {
        if (!row.Satisfied)
            return new CriterionAccount(row.Criterion, false, null, row.Note);

        if (index.Contains(row.Citation))
            return new CriterionAccount(row.Criterion, true, row.Citation, row.Note);

        logger.LogWarning(
            "{Repo}: criterion '{Criterion}' was claimed satisfied by '{Citation}', which is not in the diff",
            repoKey, Shorten(row.Criterion), row.Citation ?? "(nothing)");
        return new CriterionAccount(
            row.Criterion, false, null,
            $"claimed satisfied by '{row.Citation ?? "(nothing cited)"}', which the diff does not touch");
    }

    private const string RoleName = "spec-accountant";

    private async Task<IReadOnlyList<AccountRow>?> AskAsync(
        IChatClient chat, string repoKey, IReadOnlyList<string> criteria,
        string diff, PipelineCostTracker costTracker, CancellationToken ct)
    {
        var prompt = SpecAccountPrompt.For(criteria, diff);
        try
        {
            // The account is a model call like any other: it belongs in the cost ledger
            // and in the run trail, under its own role, or it is spend nobody can see.
            using var _ = costTracker.BeginCall(
                RoleName, RoleName, SkillExecutionPhase.Verify, repoKey);
            using var _scope = runContext.BeginCallScope(
                RoleName, SkillExecutionPhase.Verify.ToString(), repoKey);
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)], new ChatOptions(), ct);
            costTracker.Track(response);
            return SpecAccountReader.Read(response.Text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The accounting call failed");
            return null;
        }
    }

    private static string Shorten(string text) =>
        text.Length <= 60 ? text : text[..60] + "…";

}
