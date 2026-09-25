using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-25-d0b8: the answer to "what epic is this ticket stamped into?" — the ground when
/// the parent was opened, and the note that says why there is none when it was not.
/// <para>
/// The read USED to hand back a pipeline key plus a run step's sentence, which is why nothing
/// outside a pipeline could call it. Both halves live here instead: <see cref="Ground"/> is
/// what a reader needs, <see cref="Note"/> is what a caller SAYS about the read, carrying no
/// step-message punctuation of its own — the run's step adds its own separator.
/// </para>
/// </summary>
/// <param name="Ground">The parent as its readers see it, or null when there is none to read.</param>
/// <param name="Note">
/// Empty for the ordinary ticket that is nobody's slice, so a caller outside an epic says
/// exactly what it always said. Non-empty with a null <see cref="Ground"/> is the DEGRADED
/// read: a stamped parent that could not be opened.
/// </param>
public sealed record EpicParentRead(EpicGround? Ground, string Note)
{
    /// <summary>The ticket carries no <c>phase-parent:</c> stamp — nothing was looked up.</summary>
    public static readonly EpicParentRead Unstamped = new(null, string.Empty);

    public static EpicParentRead Opened(EpicGround ground)
    {
        ArgumentNullException.ThrowIfNull(ground);
        return new(ground, $"epic ground read from parent {ground.ParentTicketId}");
    }

    /// <summary>
    /// A stamped parent that could not be opened. It may be deleted, moved or invisible to this
    /// token, and the child's own ticket is still a complete requirement — so every caller
    /// degrades, and for the one reason, rather than inventing a second answer to the question.
    /// </summary>
    public static EpicParentRead Degraded(string parentId, string reason) =>
        new(null, $"epic parent {parentId} could not be read ({reason}); "
            + "this run proceeds on its own ticket");
}
