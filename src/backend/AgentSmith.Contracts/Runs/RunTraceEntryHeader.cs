namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0423b: one entry of a recorded conversation, WITHOUT its content — the position, the
/// kind ("prompt", "answer", "tool") and how big it is.
/// <para>
/// Prompts reach megabytes, so the list of what is readable must never carry what is
/// readable. The reader lists headers and fetches one entry at a time.
/// </para>
/// </summary>
public sealed record RunTraceEntryHeader(int Sequence, string Label, int Chars);
