namespace AgentSmith.Contracts.Commands;

/// <summary>2026-10-26-bdd0c/d: the keys of a spec derivation, the act that derives a ticket's specs without
/// attempting its work.</summary>
public static partial class ContextKeys
{
    /// <summary>2026-10-26-bdd0c: the sentence a spec derivation ended on (string): where its specs are, or why it
    /// stopped. The executor stops on it and the sentence becomes the run's summary; it is never a park.</summary>
    public const string SpecDerivationEnded = "SpecDerivationEnded";

    /// <summary>2026-10-26-bdd0d: true when the person asked for a preview — the specs are derived, reviewed and
    /// recorded on the run, and nothing is written to the branch, a pull request or the ticket.</summary>
    public const string SpecDerivationPreview = "SpecDerivationPreview";

    /// <summary>2026-10-26-bdd0g: <c>ISpecDerivationStart</c> — deriving the bound ticket's specs as the turn's poster.
    /// Seeded only for a poster holding runs.control, so no other turn carries derive_specs.</summary>
    public const string SpecDialogDerivationStart = "SpecDialogDerivationStart";
}
