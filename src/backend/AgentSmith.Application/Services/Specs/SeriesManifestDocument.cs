namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-06-03c7d: <c>series/{base}.yaml</c> — what the next RUN reads about a series, beside the
/// spec files a HUMAN reads. It replaces <c>set.yaml</c> and <c>accounting.md</c>: the ticket key it
/// is found by, the goal, the ordered spec IDS (never stems — a spec's label is read off its file
/// name), the revisions, the accounting, the hand-back, the fingerprint and the approval.
/// <para>
/// Executed ids are recorded here because an executed spec is APPEND-ONLY: editing one would
/// rewrite the record of work that already happened and already sits in the branch history.
/// </para>
/// </summary>
public sealed class SeriesManifestDocument
{
    /// <summary>The ticket key the reader finds this manifest by.</summary>
    public string Ticket { get; set; } = string.Empty;

    public string? Goal { get; set; }
    public bool TicketPinnedWhole { get; set; }
    public List<string> Specs { get; set; } = [];
    public List<string> ExecutedSpecs { get; set; } = [];
    public List<SeriesRevisionEntry> Revisions { get; set; } = [];
    public List<SeriesCarriedEntry> Carried { get; set; } = [];
    public List<SeriesDiscardedEntry> Discarded { get; set; } = [];
    public List<int> Unaccounted { get; set; } = [];
    public List<SeriesDiscardedContextEntry> DiscardedContexts { get; set; } = [];
    public string? HandbackCase { get; set; }
    public string? HandbackReason { get; set; }
    public List<string> HandbackReadings { get; set; } = [];
    public int HandbackTaken { get; set; }

    /// <summary>2026-09-08-5cd2: the ticket text this revision was cut from.</summary>
    public string? TicketFingerprint { get; set; }

    /// <summary>2026-09-17-0e79a: the approval this published series came from — the instant, the
    /// conversation and the principal; absent on every series nobody approved.</summary>
    public string? ApprovedAt { get; set; }

    /// <inheritdoc cref="ApprovedAt"/>
    public string? ApprovedInConversation { get; set; }

    /// <inheritdoc cref="ApprovedAt"/>
    public string? ApprovedBy { get; set; }
}
