namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0427: the one place that knows how a trace entry is named — <c>trace/0007.answer</c>.
/// <para>
/// The writer formats through it and the reader parses through it, so a replay can never
/// drift from the recording it is meant to reproduce.
/// </para>
/// </summary>
public static class RecordedTraceKey
{
    public const string Prefix = "trace/";

    public static string Format(int sequence, string label) => $"{Prefix}{sequence:D4}.{label}";

    /// <summary>File name of an exported entry — the key without its store prefix.</summary>
    public static string FileName(RecordedTraceEntry entry) => $"{entry.Sequence:D4}.{entry.Label}";

    public static bool TryParse(string key, string content, out RecordedTraceEntry entry)
    {
        entry = new RecordedTraceEntry(0, string.Empty, content);
        var name = key.StartsWith(Prefix, StringComparison.Ordinal) ? key[Prefix.Length..] : key;
        var dot = name.IndexOf('.');
        if (dot <= 0 || dot == name.Length - 1) return false;
        if (!int.TryParse(name[..dot], out var sequence)) return false;
        entry = new RecordedTraceEntry(sequence, name[(dot + 1)..], content);
        return true;
    }
}
