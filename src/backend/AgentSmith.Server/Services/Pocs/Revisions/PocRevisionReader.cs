using System.Security.Claims;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Pocs.Revisions;

/// <summary>
/// 2026-11-23-933dc: one revision of a PoC for its page. The route already required runs.read in the PoC's project, so
/// a caller who reaches this may read it; may_overrule is true only for a holder of pocs.overrule when the revision is
/// the latest, blocked, and every block is a vulnerability — why_not names the first that fails.
/// </summary>
internal sealed class PocRevisionReader(PocRepository pocs, PocScanRepository revisions, PocOverruleRule rule, ProjectAccess access)
{
    public const string NeedsOverrule = "a holder of pocs.overrule who can read this PoC's runs can lift this block";
    private static readonly string[] Overrule = [Permissions.PocsOverrule];

    public async Task<PocRevisionAnswer?> ReadAsync(ClaimsPrincipal user, long pocId, int revision, CancellationToken ct)
    {
        if (await pocs.FindAsync(pocId, ct) is not { } poc || await revisions.RowAsync(pocId, revision, ct) is not { } found)
            return null;
        var (row, latest) = found;
        var whyNot = rule.WhyNot(row, latest)
            ?? (!access.Enforcing || access.HoldsEverywhere(user, Overrule) ? null : NeedsOverrule);
        return new PocRevisionAnswer(poc.Id, poc.Name, row.Number, latest, row.State, row.RunId, row.CommitSha,
            PocScanVerdict.FromJson(row.VerdictJson), row.Explanation, whyNot is null, whyNot);
    }
}
