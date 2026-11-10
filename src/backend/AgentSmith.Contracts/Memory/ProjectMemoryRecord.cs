namespace AgentSmith.Contracts.Memory;

/// <summary>
/// 2026-11-10-fcd7a: one memory a project holds, with where it came from. <paramref name="Type"/> is the entry
/// type's slug (feedback, project, reference). <paramref name="SourceKind"/> is
/// <see cref="ProjectMemorySourceKinds"/>; <paramref name="SourceId"/> the conversation or run;
/// <paramref name="SourceRef"/> the transcript index or spec id; <paramref name="Author"/> who said or wrote it.
/// </summary>
public sealed record ProjectMemoryRecord(
    string Slug,
    string Type,
    string Description,
    string Body,
    string SourceKind,
    string SourceId,
    string? SourceRef,
    string? Author,
    DateTimeOffset CreatedAt = default,
    DateTimeOffset UpdatedAt = default);

/// <summary>
/// 2026-11-10-fcd7a: the project store's names. <see cref="Label"/> is how a tool and a citation name it — no
/// repository may start with '@' — and <see cref="LedgerKey"/> keys its rows in the memory ledger.
/// </summary>
public static class ProjectMemorySource
{
    public const string Label = "@project";

    public static string LedgerKey(long projectId) => $"project-memory:{projectId}";
}

/// <summary>2026-11-10-fcd7a: what wrote a project memory.</summary>
public static class ProjectMemorySourceKinds
{
    public const string Conversation = "conversation";
    public const string Run = "run";
}

/// <summary>
/// 2026-11-10-fcd7a: entries reach later prompts unreviewed, so their text is bounded and refused above the
/// bound rather than cut.
/// </summary>
public static class ProjectMemoryLimits
{
    public const int Description = 200;
    public const int Body = 4000;

    /// <summary>Why <paramref name="record"/> cannot be stored; null when it can.</summary>
    public static string? Refusal(ProjectMemoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Description.Length > Description)
            return $"the description has {record.Description.Length} characters; at most {Description} are kept";
        return record.Body.Length > Body
            ? $"the body has {record.Body.Length} characters; at most {Body} are kept"
            : null;
    }
}
