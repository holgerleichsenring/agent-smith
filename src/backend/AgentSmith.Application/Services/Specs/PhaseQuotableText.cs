using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-15-a5a5: what a cut reviewer may quote from a phase, and what admits a quote.
/// <para>
/// The goal always, and the done-list when the phase states one; otherwise the goal and the
/// step actions — a draft with a goal and steps and no criteria is still a plan that can rest
/// on a false premise. A blank done entry (the reader's rendering of a null YAML item) states
/// nothing, and a step whose action is its own id (the reader's rendering of a step with no
/// action) states nothing either.
/// </para>
/// <para>
/// A quote is compared word by word, never by characters. It is admitted when it IS a stated
/// text, whatever its length — "Builds" is a criterion — or when it is a contiguous run of at
/// least <see cref="MinWords"/> of a stated text's words covering at least half of them. A
/// quote that merely CONTAINS a stated text is not admitted: an invented sentence can contain
/// any short criterion.
/// </para>
/// </summary>
public static class PhaseQuotableText
{
    /// <summary>Fewer words than this, short of the whole stated text, identify nothing.</summary>
    public const int MinWords = 2;

    /// <summary>True when the phase states a done-list.</summary>
    public static bool StatesCriteria(PhaseDraft draft) =>
        draft.Done.Any(d => !string.IsNullOrWhiteSpace(d));

    /// <summary>The goal, then the done entries — or the step actions of a phase that states none.</summary>
    public static IReadOnlyList<string> Of(PhaseDraft draft) =>
        [draft.Goal, .. Statements(draft)];

    /// <summary>True when the quote is, or is most of, one of the phase's quotable texts.</summary>
    public static bool Contains(PhaseDraft draft, string? quoted)
    {
        var quote = Words(quoted);
        return quote.Length > 0 && Of(draft).Any(stated => Matches(Words(stated), quote));
    }

    /// <summary>The phase as the reviewer is shown it, with no heading over nothing.</summary>
    public static string Render(PhaseDraft draft)
    {
        var head = $"phase_id: {draft.PhaseId}\ngoal: {draft.Goal}";
        var statements = Statements(draft);
        if (statements.Count == 0) return head;
        return head + (StatesCriteria(draft) ? "\ndone:\n" : "\nsteps:\n")
            + string.Join("\n", statements.Select(line => "  - " + line));
    }

    private static IReadOnlyList<string> Statements(PhaseDraft draft) =>
        StatesCriteria(draft)
            ? [.. draft.Done.Where(d => !string.IsNullOrWhiteSpace(d))]
            : [.. draft.Steps.Where(s => !string.Equals(s.Action, s.Id, StringComparison.Ordinal))
                .Select(s => s.Action)];

    private static bool Matches(string[] stated, string[] quote)
    {
        if (stated.Length == 0) return false;
        if (stated.SequenceEqual(quote)) return true;
        if (quote.Length < MinWords || quote.Length * 2 < stated.Length) return false;
        for (var i = 0; i + quote.Length <= stated.Length; i++)
            if (stated.AsSpan(i, quote.Length).SequenceEqual(quote)) return true;
        return false;
    }

    private static string[] Words(string? text) =>
        [.. Regex.Matches(text ?? string.Empty, @"[\p{L}\p{N}]+").Select(m => m.Value.ToLowerInvariant())];
}
