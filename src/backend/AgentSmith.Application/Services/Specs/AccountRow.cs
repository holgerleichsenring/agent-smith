namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0420: one row exactly as the accounting instance wrote it, before its citation has
/// been resolved against the diff. Separate from CriterionAccount because a CLAIM and a
/// CHECKED claim are not the same thing, and the whole gate rests on the difference.
/// </summary>
/// <param name="Citation">A single citation, the shape written before p0474.</param>
/// <param name="Citations">
/// p0474: the citations as a LIST, because a citation carries shell text and every
/// separator that could join two of them — semicolon, pipe, ampersand, newline — occurs
/// inside commands. One element is one whole thing.
/// </param>
public sealed record AccountRow(
    string Criterion,
    bool Satisfied,
    string? Citation = null,
    string? Note = null,
    IReadOnlyList<string>? Citations = null)
{
    /// <summary>Everything this row cites, whichever field carried it. A bare string reads
    /// as a one-element list so an older answer still resolves.</summary>
    public IReadOnlyList<string> Cited =>
        Citations is { Count: > 0 } list
            ? [.. list.Where(c => !string.IsNullOrWhiteSpace(c))]
            : string.IsNullOrWhiteSpace(Citation) ? [] : [Citation];
}
