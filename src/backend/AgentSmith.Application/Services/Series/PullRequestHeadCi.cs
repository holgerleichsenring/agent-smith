using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-25-9992a: the one rule a pull request's CI is read by — on the series page's rows, in a ticket's hold and in the
/// series master's facts alike. A record's stored CI describes its head when it was read on that head; a read on an older
/// head is pending since the head moved (the new head's CI is not read yet); a record with no read has none. Several heads
/// aggregate by <see cref="Precedence"/>: red, then a build that never started (nothing finishes it by itself), then
/// pending, then green, then none.
/// </summary>
public static class PullRequestHeadCi
{
    public static IReadOnlyList<string> Precedence { get; } = [CiStates.Red, CiStates.NotStarted, CiStates.Pending, CiStates.Green, CiStates.None];

    /// <summary>The record's CI on its current head; null before any CI read.</summary>
    public static PrRecordCiView? OnHead(PrReviewRecordView record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Ci switch
        {
            null => null,
            { } read when read.IsOn(record.HeadSha) => read,
            _ => new PrRecordCiView(CiStates.Pending, record.HeadSha, record.HeadMovedAt, []),
        };
    }

    /// <summary>The state several heads read as together; null when none was read.</summary>
    public static string? Of(IEnumerable<string?> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        var read = states.OfType<string>().ToHashSet(StringComparer.Ordinal);
        return Precedence.FirstOrDefault(read.Contains);
    }

    /// <summary>A red head's first failed check and the first line of its excerpt.</summary>
    public static string? FirstFailure(PrRecordCiView? ci) =>
        ci is { State: CiStates.Red } && ci.Failures.FirstOrDefault() is { } f ? $"{f.Name}: {f.Excerpt.Split('\n', 2)[0].Trim()}" : null;
}
