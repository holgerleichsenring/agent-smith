namespace AgentSmith.Application.Services.Events;

/// <summary>
/// Bounded ring of a command's most recent output lines. Keeps at most
/// <see cref="MaxLines"/> lines and <see cref="MaxChars"/> characters so a
/// captured failure tail stays compact — it must never grow into a second
/// firehose. One instance per command execution.
/// </summary>
public sealed class OutputTailBuffer
{
    private const int MaxLines = 40;
    private const int MaxChars = 4000;
    private readonly Queue<string> _lines = new();
    private int _chars;

    /// <summary>
    /// p0423: everything the command ever emitted, counted as it streams past. The tail
    /// is what is KEPT; this is what there WAS — a build that printed four megabytes and
    /// a build that printed four lines look identical from a 40-line tail.
    /// </summary>
    public long TotalChars { get; private set; }

    public void Add(string line)
    {
        TotalChars += line.Length + 1;
        _lines.Enqueue(line);
        _chars += line.Length + 1;
        while (_lines.Count > MaxLines || _chars > MaxChars)
            _chars -= _lines.Dequeue().Length + 1;
    }

    /// <summary>Rendered tail, or null when nothing was captured.</summary>
    public string? Render() => _lines.Count == 0 ? null : string.Join('\n', _lines);
}
