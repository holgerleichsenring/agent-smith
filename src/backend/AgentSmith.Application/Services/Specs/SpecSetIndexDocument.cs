using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: <c>set.yaml</c> — the machine-readable index beside the phase specs.
/// The phase yaml and its markdown companion are what a HUMAN reads; the order of
/// the sequence, the revision history, the accounting and which phases already
/// executed are what the next RUN reads, and putting them in the phase files would
/// mean editing a phase spec to record something that is not part of the phase.
/// <para>
/// Executed phase ids are recorded here because an executed phase is APPEND-ONLY:
/// editing one would rewrite the record of work that already happened and already
/// sits in the branch history.
/// </para>
/// <para>
/// A question hand-back keeps its readings and the taken index here, so the next run
/// can name the reading it proceeds on from the question that was actually asked.
/// </para>
/// </summary>
public sealed class SpecSetIndexDocument
{
    public string Key { get; set; } = string.Empty;
    public string Source { get; set; } = SpecSource.Derived.ToString();
    public bool TicketPinnedWhole { get; set; }
    public List<string> Phases { get; set; } = [];
    public List<string> ExecutedPhases { get; set; } = [];
    public List<SpecSetRevisionEntry> Revisions { get; set; } = [];
    public List<SpecSetCarriedEntry> Carried { get; set; } = [];
    public List<SpecSetDiscardedEntry> Discarded { get; set; } = [];
    public List<int> Unaccounted { get; set; } = [];
    public string? HandbackCase { get; set; }
    public string? HandbackReason { get; set; }
    public List<string> HandbackReadings { get; set; } = [];
    public int HandbackTaken { get; set; }
}

public sealed class SpecSetRevisionEntry
{
    public int Number { get; set; }
    public string Cause { get; set; } = string.Empty;
    public string At { get; set; } = string.Empty;
}

public sealed class SpecSetCarriedEntry
{
    public int Segment { get; set; }
    public string Phase { get; set; } = string.Empty;
}

public sealed class SpecSetDiscardedEntry
{
    public int Segment { get; set; }
    public string Reason { get; set; } = string.Empty;
}
