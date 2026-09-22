using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-22-355b: the other shapes a proposal could take, named by the framework from the
/// proposal's own KIND. The three kinds that reach a confirmation are a ladder by size — a
/// fix-bug ticket, one phase, an epic of linked slices — and each one offers its neighbours,
/// so the middle kind offers two and the ends offer one.
/// <para>
/// The code decides nothing about the cut: a picked shape is fed into the edit door that
/// already exists, the master re-proposes in that shape, and the operator approves THAT.
/// Nothing is stored either — the shapes are derived every time the confirmation is built.
/// </para>
/// <para>
/// The labels are the ANSWER KEY, so they are written here and nowhere else. One chat surface
/// answers a button with the tail of its action id after the LAST colon, which is why a label
/// may not contain one; and the confirmer reads an approval or rejection word before it reads
/// anything else, which is why a label may not be one. Both are enforced below, where they
/// are written, and pinned by tests that read this table rather than a copy of it.
/// </para>
/// </summary>
public static class OutcomeShapes
{
    /// <summary>
    /// The label ceiling every surface is known to survive. A question id is a thirty-two
    /// character identifier, so a colon plus a label of at most this length is at most
    /// seventy-three characters of action id.
    /// </summary>
    public const int MaxLabelLength = 40;

    private static readonly DialogChoice IntoSeveralPhases =
        new("Cut into several phases", "too big for one phase");
    private static readonly DialogChoice IntoOnePhase =
        new("Make it one phase", "over-cut for the work");
    private static readonly DialogChoice IntoAPhase =
        new("Make it a phase", "bigger than a bug fix");
    private static readonly DialogChoice IntoABugTicket =
        new("Make it a bug ticket", "smaller than a phase");

    /// <summary>Every shape the framework offers, so a bound is taken over all of them at once.</summary>
    public static IReadOnlyList<DialogChoice> All { get; } =
        [IntoSeveralPhases, IntoOnePhase, IntoAPhase, IntoABugTicket];

    static OutcomeShapes()
    {
        foreach (var shape in All)
        {
            if (shape.Label.Contains(':', StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Offered shape '{shape.Label}' contains a colon; a chat surface would split "
                    + "its action id in the wrong place and drop the click in silence.");
            if (shape.Label.Length > MaxLabelLength)
                throw new InvalidOperationException(
                    $"Offered shape '{shape.Label}' is longer than {MaxLabelLength} characters.");
            if (SpecDialogAnswerWords.DecisionIn(shape.Label) is not null)
                throw new InvalidOperationException(
                    $"Offered shape '{shape.Label}' reads as an approval or rejection; the "
                    + "confirmer would decide on it before it could be taken as a direction.");
        }
    }

    /// <summary>The shapes offered beside approve and reject for this proposal.</summary>
    public static IReadOnlyList<DialogChoice> For(OutcomeProposal proposal) => proposal switch
    {
        BugOutcome => [IntoAPhase],
        PhaseOutcome => [IntoSeveralPhases, IntoABugTicket],
        EpicOutcome => [IntoOnePhase],
        // An answer never reaches a confirmation, and a kind added later offers nothing until
        // it says what its neighbours are.
        _ => [],
    };
}
