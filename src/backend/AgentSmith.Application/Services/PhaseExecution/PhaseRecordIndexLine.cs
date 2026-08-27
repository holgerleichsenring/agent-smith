namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// 2026-08-26-31e5: composes the <c>state.done</c> index line for a finished phase — the
/// pointer to the record file, and as much of the goal as the cap allows, cut at a word
/// boundary.
/// <para>
/// It is composed TO FIT rather than refused for not fitting: the record step runs after the
/// work is committed, so refusing an over-long line would fail a run nobody could then go
/// back and shorten. Measured over the specs in this repository, one goal in seven exceeds
/// the cap. Only a pointer that alone will not fit is an error worth stopping for.
/// </para>
/// <para>
/// This is the ONE place the cap lives. PhaseRecordLengthRatchetTests guards this repository
/// and the schema describes the field; a target repository has neither, so the number that
/// matters is the one the writer composes against.
/// </para>
/// </summary>
public sealed class PhaseRecordIndexLine
{
    /// <summary>p0512: an index line, not an essay — what shipped, where, and the pointer.</summary>
    public const int MaxChars = 400;

    private const string Arrow = " -> ";
    private const char Ellipsis = '…';

    /// <summary>Null when even the bare pointer exceeds the cap.</summary>
    public string? Compose(string goal, string pointer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pointer);
        if (pointer.Length > MaxChars) return null;

        var budget = MaxChars - Arrow.Length - pointer.Length;
        var text = string.Join(' ', (goal ?? string.Empty).Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length == 0 || budget < 2) return pointer;
        return (text.Length <= budget ? text : Cut(text, budget - 1) + Ellipsis) + Arrow + pointer;
    }

    private static string Cut(string text, int room)
    {
        var head = text[..room];
        var space = head.LastIndexOf(' ');
        if (space > 0) head = head[..space];
        return head.TrimEnd(' ', ',', ';', ':', '.', '-');
    }
}
