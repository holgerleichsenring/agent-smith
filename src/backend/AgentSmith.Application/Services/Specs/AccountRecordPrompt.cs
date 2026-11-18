namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-18-d6d2c: the record sections of a verification prompt. The spec as ratified always; after a repair, the
/// previous findings with the carry-over rule and the files the repair changed, so the verifier keeps its word: each
/// previous finding is said closed or open, and a gap it missed before on code the repair did not touch is advice.
/// </summary>
public static class AccountRecordPrompt
{
    public static string Render(AccountRecord? record)
    {
        if (record is null) return string.Empty;
        var spec = string.IsNullOrWhiteSpace(record.SpecYaml)
            ? string.Empty
            : $"\n\nTHE SPEC AS RATIFIED (its steps say what was planned; judge only its criteria)\n{record.SpecYaml.TrimEnd()}";
        return record.IsAfterRepair ? spec + CarryOver(record) : spec;
    }

    private static string CarryOver(AccountRecord record)
    {
        var changed = record.RepairChanged.Count == 0
            ? "(the repair changed no file)"
            : string.Join("\n", record.RepairChanged.Select(p => "- " + p));
        return $"""


            THE PREVIOUS VERIFICATION OF THIS SPEC found these not satisfied, and the repair pass was handed exactly
            this list:
            {string.Join("\n", record.PreviousFindings.Select(f => "- " + f))}

            Keep your word. In the note of each criterion on that list, say for every finding of it whether it is now
            CLOSED or still OPEN. A gap that is not on the list is NEW, and the note says why: it lies in a file the
            repair changed (listed below), or it was missed before. A new gap in a file the repair changed counts like
            any gap. A gap missed before, in code the repair did not change, is not a reason to stop now: when every
            listed finding of the criterion is closed, answer it satisfied — with a citation that resolves, as always —
            and put that gap in "advice".

            FILES THE REPAIR CHANGED
            {changed}
            """;
    }
}
