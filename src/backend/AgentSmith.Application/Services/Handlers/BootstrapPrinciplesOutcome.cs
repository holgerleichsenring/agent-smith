using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-08-28-7675: what a bootstrap round records about the principles it produced.
/// Extracted from <see cref="BootstrapRoundHandler"/>, which orchestrates the round —
/// three modes with three different sentences is its own responsibility, and the
/// handler is over the length its ratchet allows.
/// <para>
/// The mode where the SKILL authored the principles is the one that needs explaining:
/// it happens because the resolved catalog carried no principles core, and naming that
/// catalog is what tells a deliberate configuration from a mispointed mount.
/// </para>
/// </summary>
internal static class BootstrapPrinciplesOutcome
{
    public static string Sentence(
        PrinciplesTransferResult transfer, string displayName, bool retiredRenamed = false) =>
        transfer.Mode switch
        {
            PrinciplesMode.Transferred =>
                $"{displayName} [Bootstrap]: context.yaml written; coding principles "
                + "transferred from the authored core+delta"
                + string.Concat(transfer.Overlays.Select(o => $"+{o}")) // 2026-10-03-cf20c
                + " (operator ratifies via the init PR)",
            PrinciplesMode.PreservedExisting =>
                $"{displayName} [Bootstrap]: context.yaml written; coding principles "
                + "preserved (ratified content is never overwritten)",
            // 2026-10-04-2bf2: the operator asked for this replacement on the launch.
            PrinciplesMode.Refreshed =>
                $"{displayName} [Bootstrap]: context.yaml written; coding principles "
                + "refreshed from the authored core+delta"
                + string.Concat(transfer.Overlays.Select(o => $"+{o}"))
                + (transfer.ProjectSpecificsKept
                    ? " with the Project Specifics carried over"
                    : " (no Project Specifics section to carry over)")
                + " (operator ratifies via the init PR)",
            _ => throw new ArgumentOutOfRangeException(
                nameof(transfer), transfer.Mode, "SkillWrites is reported by SkillWroteThem"),
        } + RenameNote(retiredRenamed);

    public static string SkillWroteThem(
        PrinciplesTransferResult transfer, string displayName, int changes,
        bool retiredRenamed = false) =>
        $"{displayName} [Bootstrap]: {changes} file(s) written; coding principles authored "
        + $"by the skill — catalog {transfer.CatalogOrigin ?? "unresolved"} shipped no principles core"
        + RenameNote(retiredRenamed);

    // 2026-09-01-72c5: the migration is stated in the round's own result, so an operator
    // reading the init pull request sees the rename instead of inferring it from the diff.
    private static string RenameNote(bool retiredRenamed) =>
        retiredRenamed
            ? $"; {ProjectMetaPaths.RetiredPrinciplesFile} was renamed to "
              + $"{ProjectMetaPaths.PrinciplesFile} before the round read it"
            : string.Empty;
}
