using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Pocs.Revisions;

/// <summary>
/// 2026-11-23-933dc: why a revision's block cannot be overruled, or null when it can — read by the page's
/// may_overrule/why_not and by the overrule route alike, so both say the same. 2026-11-23-933dd: the verdict read is the one of the stage BlockedStage names.
/// </summary>
internal sealed class PocOverruleRule
{
    public const string SecretBlock = "a secret cannot be overruled: remove it and upload again";

    public string? WhyNot(PocRevision row, int latest)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.State != PocRevisionStates.Blocked) return $"revision {row.Number} is not blocked";
        if (row.Number != latest) return $"revision {row.Number} is older than r{latest}";
        return Blocking(row) is { Overrulable: true } ? null : SecretBlock;
    }

    /// <summary>The verdict of the stage the revision is blocked at — the image scan's for 'image', else the code scan's.</summary>
    public static PocScanVerdict? Blocking(PocRevision row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return PocScanVerdict.FromJson(row.BlockedStage == PocScanStages.Image ? row.ImageVerdictJson : row.VerdictJson);
    }
}
