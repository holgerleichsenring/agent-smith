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
                + "transferred from the authored core+delta (operator ratifies via the init PR)",
            PrinciplesMode.PreservedExisting =>
                $"{displayName} [Bootstrap]: context.yaml written; coding principles "
                + "preserved (ratified content is never overwritten)",
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
