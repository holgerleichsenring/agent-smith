namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0452: the commands the agent itself ran in this phase, as evidence the account can read.
/// <para>
/// The delivery account judges from the branch diff and a list of commands — and that list
/// held only the stages VERIFICATION ran, never the agent's own. Three live runs died on
/// that blindness with the same verdict: 459d "no listed command ran the required search",
/// 587c "covers the Server repository only", 929f "records a planned scan but provides no
/// completed scan". In 929f the agent had run nineteen searching commands across both
/// repositories, two of them printing a labelled legacy-reference report. None was visible
/// to the reader asked to judge whether a search happened.
/// </para>
/// <para>
/// This does not decide anything. A wrongly scoped search is still weak evidence and the
/// account must still say so — the point is that it can see it at all.
/// </para>
/// </summary>
public sealed class PhaseCommandLog
{
    private readonly Lock _sync = new();
    private int _ran;

    /// <summary>p0469: public and settable so the checkpoint can carry the log through a park
    /// and give it back on resume. Recorded through <see cref="Record"/>, which is what keeps
    /// it inside <see cref="PhaseCommandBudget"/>.</summary>
    public List<PhaseCommandEntry> Entries { get; init; } = [];

    /// <summary>p0470: how many commands ran, counted where they are recorded and never
    /// derived from <see cref="Entries"/> — a count taken from the list it describes would
    /// degrade with it, and the notice exists precisely to say the list degraded.</summary>
    public int Ran { get => _ran; init => _ran = value; }

    /// <summary>Runs the command and records it — the call site stays one line, and no
    /// caller has to remember to log.</summary>
    public async Task<string> RecordAsync(string? repo, string command, Func<Task<string>> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var output = await run();
        Record(repo, command, output);
        return output;
    }

    public void Record(string? repo, string command, string? output)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        lock (_sync)
        {
            _ran++;
            Entries.Add(new PhaseCommandEntry(repo ?? string.Empty,
                PhaseCommandBudget.Capped(command.Trim()),
                PhaseCommandBudget.Tail(output), ExitCode(output)));
            PhaseCommandBudget.Fit(Entries);
        }
    }

    /// <summary>One line per command, in the order they ran, for the account's prompt — led
    /// by the notice when the budget took something, so the reader never mistakes a record
    /// that was shortened for the whole of what the agent did. Pure: reading twice reads the
    /// same, because the budget shrinks at record time and never here.</summary>
    public IReadOnlyList<string> Evidence()
    {
        lock (_sync)
        {
            var notice = PhaseCommandBudget.Notice(Entries, _ran);
            return notice is null ? [.. Entries.Select(Line)] : [notice, .. Entries.Select(Line)];
        }
    }

    /// <summary>p0469: the same shape a verification stage gets — "'<c>cmd</c>' exited N".
    /// A search that proves an absence exits non-zero BECAUSE it found nothing, and a
    /// reader that cannot see the status cannot tell that from a search that never ran.
    /// </summary>
    private static string Line(PhaseCommandEntry entry) =>
        (entry.Repo.Length > 0 ? $"{entry.Repo}: " : string.Empty)
        + $"the agent ran '{entry.Command}' "
        + (entry.ExitCode is { } code ? $"exited {code}" : "exit status not recorded")
        + Output(entry);

    /// <summary>p0470: an output the budget took is NOT an output that was never produced.
    /// "no output" is the proof a search found nothing, pinned by p0452's own test, so a
    /// trimmed tail borrowing that wording would make this phase's defect one level down.
    /// </summary>
    private static string Output(PhaseCommandEntry entry) =>
        entry.OutputTrimmed ? " — output not shown (trimmed to fit)"
        : entry.Tail.Length > 0 ? $" — output: {entry.Tail}" : " — no output";

    /// <summary>run_command reports its status in an <c>exit_code:</c> header, and the tail
    /// the account reads starts well past it.</summary>
    private static int? ExitCode(string? output)
    {
        const string header = "exit_code:";
        if (output is null) return null;
        var text = output.AsSpan().TrimStart();
        if (!text.StartsWith(header, StringComparison.Ordinal)) return null;
        var end = text.IndexOf('\n');
        var value = end < 0 ? text[header.Length..] : text[header.Length..end];
        return int.TryParse(value.Trim(), out var code) ? code : null;
    }
}
