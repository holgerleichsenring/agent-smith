namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0427: a recorded run, read back in the order it was written.
/// <para>
/// A replay drives the framework from <see cref="Answers"/> alone: the prompts and tool
/// entries describe what the model SAW, which the framework composes again for itself when
/// the recording is replayed. They are kept because they are the evidence a human reads.
/// </para>
/// </summary>
public sealed class RecordedTrace
{
    public const string AnswerLabel = "answer";
    public const string PromptLabel = "prompt";

    public static RecordedTrace Empty { get; } = new([]);

    public IReadOnlyList<RecordedTraceEntry> Entries { get; }

    private RecordedTrace(IReadOnlyList<RecordedTraceEntry> entries) => Entries = entries;

    public static RecordedTrace Of(IEnumerable<RecordedTraceEntry> entries) =>
        new([.. entries.OrderBy(e => e.Sequence)]);

    /// <summary>The answers a replay serves, in the order the run received them.</summary>
    public IReadOnlyList<string> Answers =>
        [.. Entries.Where(e => e.Label == AnswerLabel).Select(e => e.Content)];

    public bool IsEmpty => Entries.Count == 0;
}
