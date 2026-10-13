namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565c: what a bootstrap round did with a context's structure baseline.
/// <paramref name="Note"/> says why nothing was written; <paramref name="Refreshed"/> that a
/// baseline was already there; <paramref name="Unreadable"/> counts files the engine claims that
/// could not be read (over 1 MB, not UTF-8, gone).
/// </summary>
public sealed record StructureBaselineResult(
    bool Written, int Entries, int Unreadable, bool Refreshed, string? Note = null)
{
    public static StructureBaselineResult Skipped(string note, bool present = false) =>
        new(false, 0, 0, present, note);

    public static StructureBaselineResult Untouched(int entries) => new(false, entries, 0, false);

    /// <summary>The init pull request's line: what is tolerated, and what is not covered.</summary>
    public string Sentence()
    {
        var file = $"`{StructureBaselineFile.FileName}`";
        if (Note is not null)
            return Refreshed
                ? $"structure baseline not refreshed — {Note}; the existing {file} is left as it was"
                : $"no structure baseline — {Note}";
        if (!Written)
            return $"structure baseline left untouched: the preserved principles keep their {file} ({Entries} unit(s))";
        var lead = Refreshed
            ? $"structure baseline refreshed: {Entries} unit(s) over the Limits stay tolerated in {file}, "
              + "none above its old value; a unit grown past an unchanged limit surfaces at the gate"
            : $"{Entries} unit(s) already exceed the Limits and are recorded in {file}; "
              + "from here only what gets worse fails";
        return Unreadable == 0 ? lead : $"{lead}; {Unreadable} file(s) could not be read and are not covered";
    }
}
