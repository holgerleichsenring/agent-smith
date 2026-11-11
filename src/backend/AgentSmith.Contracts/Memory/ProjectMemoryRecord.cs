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
/// bound rather than cut. 2026-11-11-205fe/205fg: ONE CHECK — the description and the author are one line each, slug and
/// author fit an indexed column; it runs where the author is set, at the store's write.
/// </summary>
public static class ProjectMemoryLimits
{
    public const int Description = 200;
    public const int Body = 4000;

    /// <summary>2026-11-11-205fg: an indexed text column's bound, which Slug and Author share.</summary>
    public const int Indexed = 191;

    /// <summary>Why <paramref name="record"/> cannot be stored; null when it can.</summary>
    public static string? Refusal(ProjectMemoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Description.Length > Description)
            return $"the description has {record.Description.Length} characters; at most {Description} are kept";
        if (Breaks(record.Description)) return "the description holds a line break; it is one line";
        if (record.Slug.Length > Indexed) return $"the name has {record.Slug.Length} characters; at most {Indexed} are kept";
        if (record.Author is { } author && Breaks(author)) return "the author holds a line break; it is one line";
        if (record.Author is { Length: > Indexed } longAuthor)
            return $"the author has {longAuthor.Length} characters; at most {Indexed} are kept";
        return record.Body.Length > Body
            ? $"the body has {record.Body.Length} characters; at most {Body} are kept"
            : null;
    }

    private static bool Breaks(string text) => text.Any(c => c is '\n' or '\r' or '\u2028' or '\u2029');
}

/// <summary>2026-11-11-205fg: a project memory the store refused, with the reason a tool answers with.</summary>
public sealed class ProjectMemoryRefusedException(string reason) : ArgumentException(reason)
{
    public string Reason { get; } = reason;
}
