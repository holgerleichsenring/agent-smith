namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-12-cd4ca: the per-turn proposal store the design master writes into one item at a time. A write is
/// checked against the current draft and either replaces it whole or leaves it as it was; <see cref="Changed"/>
/// says whether this turn changed the draft at all, which is what makes the turn a proposal rather than an answer.
/// </summary>
public sealed class ProposalStore
{
    private readonly object _gate = new();

    public ProposalDraft Current { get; private set; } = ProposalDraft.Empty;

    public bool Changed { get; private set; }

    /// <summary>2026-11-19-7c42a: the last write deleted the whole draft — the operator's discard, said in words. Any
    /// later write clears it, so a draft emptied on the way to a rewrite is no discard.</summary>
    public bool Discarded { get; private set; }

    /// <summary>The draft the turn starts from; <paramref name="changed"/> when it must be published again as it is.</summary>
    public void Seed(ProposalDraft draft, bool changed)
    {
        ArgumentNullException.ThrowIfNull(draft);
        lock (_gate) (Current, Changed, Discarded) = (draft, changed, false);
    }

    /// <summary>Applies one write: the next draft, or the refusal that leaves the current one standing.
    /// <paramref name="discards"/>: the write deletes the whole draft (2026-11-19-7c42a).</summary>
    public string? Apply(Func<ProposalDraft, DraftWrite> write, bool discards = false)
    {
        ArgumentNullException.ThrowIfNull(write);
        lock (_gate)
        {
            var result = write(Current);
            if (result.Next is null) return result.Refusal ?? "not stored";
            (Current, Changed, Discarded) = (result.Next, true, discards);
            return null;
        }
    }
}
