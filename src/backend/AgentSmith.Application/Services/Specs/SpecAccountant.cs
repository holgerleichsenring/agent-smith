using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
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
    ILogger<SpecAccountant> logger)
{
    private const int MaxDiffChars = 60_000;

    public async Task<SpecAccount> AccountAsync(
        string repoKey,
        IReadOnlyList<string> criteria,
        string diff,
        AgentConfig agent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (criteria.Count == 0)
            return new SpecAccount(repoKey, [], "the phase states no completion criteria");

        var index = new DiffFileIndex(diff);
        var chat = chatClientFactory.Create(agent, TaskType.Reasoning);
        var answer = await AskAsync(chat, criteria, diff, cancellationToken);
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

    private async Task<IReadOnlyList<AccountRow>?> AskAsync(
        IChatClient chat, IReadOnlyList<string> criteria, string diff, CancellationToken ct)
    {
        var prompt = BuildPrompt(criteria, diff);
        try
        {
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)], new ChatOptions(), ct);
            return Parse(response.Text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The accounting call failed");
            return null;
        }
    }

    private static string BuildPrompt(IReadOnlyList<string> criteria, string diff)
    {
        var body = diff.Length <= MaxDiffChars
            ? diff
            : diff[..MaxDiffChars] + "\n… diff truncated; judge only what is shown and say so.";
        var list = string.Join("\n", criteria.Select(c => "- " + c));
        return $$"""
            A phase of automated work has finished. Below are the completion criteria that
            were ratified BEFORE the work started, and the complete diff the branch carries.

            Your job is to find what is MISSING. Go criterion by criterion and decide
            whether THIS DIFF satisfies it. You did not do this work and have no account of
            it other than the diff — do not assume anything happened that the diff does not
            show.

            For a criterion you call satisfied, name the file in the diff that satisfies it.
            A criterion you cannot tie to a file in the diff is NOT satisfied, whatever it
            looks like it ought to be. Saying "not satisfied" costs you nothing and is the
            useful answer; saying "satisfied" without a file is the one thing that misleads.

            Answer with JSON and nothing else:

              [{"criterion": "<verbatim>", "satisfied": true|false,
                 "citation": "<path in the diff>", "note": "<one short sentence>"}]

            CRITERIA
            {{list}}

            DIFF
            {{body}}
            """;
    }

    private static IReadOnlyList<AccountRow>? Parse(string? text)
    {
        var json = Unwrap(text);
        if (json is null) return null;
        try
        {
            return JsonSerializer.Deserialize<List<AccountRow>>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // The answer may arrive fenced or framed by prose — take the outermost array.
    private static string? Unwrap(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        return start >= 0 && end > start ? text[start..(end + 1)] : null;
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static string Shorten(string text) =>
        text.Length <= 60 ? text : text[..60] + "…";

    private sealed record AccountRow(
        string Criterion, bool Satisfied, string? Citation = null, string? Note = null);
}
