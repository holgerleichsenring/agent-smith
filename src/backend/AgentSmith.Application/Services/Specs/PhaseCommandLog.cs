using System.Text;

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
    /// <summary>Enough of the output to show what a command found, and no more: the account
    /// reads dozens of these and the diff is the bulk of its prompt already.</summary>
    internal const int TailChars = 400;

    /// <summary>A phase runs hundreds of tool calls; the account needs the shape, not a log
    /// file. Oldest are dropped — the last commands of a phase are the ones that prove it.</summary>
    internal const int MaxEntries = 40;

    private readonly Lock _sync = new();

    /// <summary>p0469: public and settable so the checkpoint can carry the log through a
    /// park and give it back on resume. Recorded through <see cref="Record"/>, which is
    /// what keeps it inside <see cref="MaxEntries"/>.</summary>
    public List<PhaseCommandEntry> Entries { get; init; } = [];

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
            Entries.Add(new PhaseCommandEntry(
                repo ?? string.Empty, command.Trim(), Tail(output), ExitCode(output)));
            if (Entries.Count > MaxEntries) Entries.RemoveAt(0);
        }
    }

    /// <summary>One line per command, in the order they ran, for the account's prompt.</summary>
    public IReadOnlyList<string> Evidence()
    {
        lock (_sync) return [.. Entries.Select(Line)];
    }

    /// <summary>p0469: the same shape a verification stage gets — "'<c>cmd</c>' exited N".
    /// A search that proves an absence exits non-zero BECAUSE it found nothing, and a
    /// reader that cannot see the status cannot tell that from a search that never ran.
    /// </summary>
    private static string Line(PhaseCommandEntry entry) =>
        (entry.Repo.Length > 0 ? $"{entry.Repo}: " : string.Empty)
        + $"the agent ran '{entry.Command}' "
        + (entry.ExitCode is { } code ? $"exited {code}" : "exit status not recorded")
        + (entry.Tail.Length > 0 ? $" — output: {entry.Tail}" : " — no output");

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

    private static string Tail(string? output)
    {
        if (string.IsNullOrWhiteSpace(output)) return string.Empty;
        var text = output.Trim();
        // The END of a command's output is where its verdict is — a grep's last matches, a
        // build's final error, the summary line of a test run.
        return text.Length <= TailChars
            ? Collapse(text)
            : "…" + Collapse(text[^TailChars..]);
    }

    private static string Collapse(string text)
    {
        var sb = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text)
        {
            var blank = char.IsWhiteSpace(c);
            if (blank && space) continue;
            sb.Append(blank ? ' ' : c);
            space = blank;
        }
        return sb.ToString().Trim();
    }
}
