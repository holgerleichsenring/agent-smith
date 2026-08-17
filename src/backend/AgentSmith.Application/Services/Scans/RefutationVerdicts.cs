using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: applies a refuter's answer to one candidate — and checks the refuter the same
/// way the refuter checks the finding.
/// <para>
/// A rebuttal must QUOTE a line of the evidence the refuter was shown — the source for a
/// repo claim, the recorded exchange for a live-target one. Anything else is the refuter
/// inventing its objection, which would silence a real critical — the one failure this
/// phase must not introduce. An unquoted rebuttal is discarded and the finding stands,
/// exactly as an unquoted cut-review finding is discarded and the cut stands.
/// </para>
/// </summary>
public sealed class RefutationVerdicts(ILogger<RefutationVerdicts> logger)
{
    /// <summary>The accepted refutation of this candidate, or null when it stands.</summary>
    public FindingRefutation? Accepted(
        CandidateFinding candidate, IReadOnlyList<FindingRefutation> answers)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(answers);
        var answer = answers.FirstOrDefault(a =>
            string.Equals(a.Location?.Trim(), candidate.Location, StringComparison.OrdinalIgnoreCase));
        if (answer is null || answer.Substantiated) return null;

        if (!Quoted(candidate, answer.Quote))
        {
            logger.LogWarning(
                "A refutation of {Location} quotes evidence it was never shown — discarding it, the finding stands: {Quote}",
                candidate.Location, Shorten(answer.Quote));
            return null;
        }

        logger.LogInformation("Refuted {Location}: {Why}", candidate.Location, answer.Why);
        return answer;
    }

    private static bool Quoted(CandidateFinding candidate, string? quote) =>
        !string.IsNullOrWhiteSpace(quote)
        && candidate.Evidence.Contains(quote.Trim(), StringComparison.Ordinal);

    private static string Shorten(string? text) =>
        text is null ? "(nothing quoted)" : text.Length <= 80 ? text : text[..80] + "…";
}
