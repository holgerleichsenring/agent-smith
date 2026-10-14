namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-13-8762o: a default a master took, or a feedback memory it proposed, waiting for a person.
/// Inserted only by the event-88 projection, one row per event, identified by its trail position; the
/// verdict columns are written only by the ratification endpoint, so a replayed event never resets one.
/// </summary>
public sealed class RunDefaultDecision : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;

    /// <summary>The trail position of the event; null when it was applied outside the drain.</summary>
    public long? EventSeq { get; set; }

    /// <summary>The spec the master executed; null outside a spec.</summary>
    public string? SpecId { get; set; }

    public string Question { get; set; } = string.Empty;

    /// <summary>The choice taken; the slug for a memory proposal.</summary>
    public string Chosen { get; set; } = string.Empty;

    public string AlternativesJson { get; set; } = "[]";
    public string Why { get; set; } = string.Empty;

    /// <summary>True holds the next automatic attempt of the series until a person decides.</summary>
    public bool HoldsSeries { get; set; }

    /// <summary><c>decision</c> or <c>memory-proposal</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The memory file a memory proposal points at, <c>{repository}/.agentsmith/memory/{slug}.md</c>.</summary>
    public string? PointerPath { get; set; }

    public DateTimeOffset TakenAt { get; set; }

    /// <summary><c>pending</c>, <c>ratified</c> or <c>overruled</c>.</summary>
    public string Verdict { get; set; } = "pending";

    public string? VerdictBy { get; set; }
    public DateTimeOffset? VerdictAt { get; set; }
    public string? VerdictNote { get; set; }

    /// <summary>The design turn's conversation; null in a coding run.</summary>
    public string? Conversation { get; set; }
}
