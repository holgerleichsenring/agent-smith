namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-11-17-a0b6d: a design conversation's proposal, one row each — the session row keeps none. A turn's proposal is
/// keyed by the turn's RUN id; one stored without a run carries an unbound key (<see cref="UnboundPrefix"/>).
/// </summary>
public sealed class SpecDialogProposal : EntityBase
{
    /// <summary>The RunId prefix of a proposal no run wrote; such a row is never a turn's.</summary>
    public const string UnboundPrefix = "proposal:";

    public long Id { get; set; }

    /// <summary>The conversation's session id.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>The turn's run id, or an unbound key; one row per key.</summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>The proposal, as OutcomeProposalJson writes it.</summary>
    public string ProposalJson { get; set; } = string.Empty;

    /// <summary>The id minted when the proposal was put to a decision; every decision carries it.</summary>
    public string? ProposalId { get; set; }

    /// <summary>Null for a turn's proposal never put to a decision; otherwise awaiting, filing, filed, rejected or
    /// superseded (the constants on SpecDialogProposalClaim). 2026-11-19-6b0bd: an older row may still read 'reviewing',
    /// a retired state nothing reads or writes; such a row is its turn's proposal, never awaiting.</summary>
    public string? State { get; set; }
}
