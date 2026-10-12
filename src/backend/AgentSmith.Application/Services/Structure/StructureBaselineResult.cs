namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565c: what a bootstrap round did with a context's structure baseline.
/// <paramref name="Note"/> says why nothing was written; <paramref name="Unreadable"/> counts
/// files the engine claims that could not be read (over 1 MB, not UTF-8, gone).
/// </summary>
public sealed record StructureBaselineResult(
    bool Written, int Entries, int Unreadable, bool Refreshed, string? Note = null)
{
    public static StructureBaselineResult Skipped(string note) => new(false, 0, 0, false, note);

    /// <summary>The init pull request's line: what is tolerated, and what is not covered.</summary>
    public string Sentence()
    {
        if (Note is not null) return $"no structure baseline — {Note}";
        if (!Written) return $"structure baseline already present and left untouched ({Entries} unit(s))";
        var lead = Refreshed
            ? $"structure baseline refreshed: {Entries} unit(s) still exceed the Limits; a limit lowered "
              + "by this refresh meets the commit gate the next time a file is touched"
            : $"{Entries} unit(s) already exceed the Limits and are recorded in "
              + $"`{StructureBaselineFile.FileName}`; from here only what gets worse fails";
        return Unreadable == 0 ? lead : $"{lead}; {Unreadable} file(s) could not be read and are not covered";
    }
}
