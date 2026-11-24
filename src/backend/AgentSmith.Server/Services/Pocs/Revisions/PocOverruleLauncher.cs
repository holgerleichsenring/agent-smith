using System.Security.Claims;
using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Configurations;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Pocs.Import;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Pocs.Revisions;

/// <summary>
/// 2026-11-23-933dc: an overrule of a blocked revision starts a run — it never writes the revision itself. It refuses a
/// blank or over-long reason, a revision that is not the latest blocked one or whose block holds a secret, a purged
/// upload (the run re-reads it), and a live run of the PoC (the run gate, 409); otherwise it claims the PoC and queues
/// poc-deploy seeded with the reason and the blocking ids, which the run appends under 'code' and scans again — or,
/// 2026-11-23-933dd, under 'image' for an image-stage block, building afresh.
/// </summary>
internal sealed class PocOverruleLauncher(
    PocRepository pocs, PocScanRepository revisions, PocOverruleRule rule, PocUploadStore uploads, PocRunGate gate,
    TicketProjects projects)
{
    public async Task<(PocOverruleAnswer? Answer, PocRefusal? Refusal)> OverruleAsync(
        ClaimsPrincipal user, long pocId, int revision, string? reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (null, PocRefusal.BadRequest("An overrule needs a reason: say why the block is lifted."));
        if (reason.Length > PocAuditEntryConfiguration.ReasonLength)
            return (null, PocRefusal.BadRequest($"The reason is {reason.Length} characters; at most {PocAuditEntryConfiguration.ReasonLength} "
                + "fit, the width of the PoC audit trail's reason column (PocAuditEntries.Reason)."));
        if (await pocs.FindAsync(pocId, ct) is not { } poc || await revisions.RowAsync(pocId, revision, ct) is not { } found)
            return (null, PocRefusal.NotFound);
        var (row, latest) = found;
        if (rule.WhyNot(row, latest) is { } whyNot) return (null, PocRefusal.BadRequest(Sentence(whyNot)));
        if (await uploads.ReadAsync(row.UploadSession, row.UploadId, ct) is null)
            return (null, PocImportRefusals.Purged(row.Number, projects.Config().Pocs.BlockedRetentionDays));
        var ids = PocOverruleRule.Blocking(row)!.Blocks.Select(b => b.Id).ToList();
        var (runId, heldBy) = await gate.QueueDeployAsync(poc, row, SpecDialogOwnership.PrincipalOf(user), ct, extraSeeds: Seeds(reason.Trim(), ids));
        return runId is null ? (null, PocImportRefusals.InProgress(poc, heldBy)) : (new PocOverruleAnswer(poc.Id, row.Number, runId), null);
    }

    private static Dictionary<string, object> Seeds(string reason, IReadOnlyList<string> ids) => new()
    {
        [ContextKeys.PocOverrule] = "true", [ContextKeys.PocOverruleReason] = reason,
        [ContextKeys.PocOverruleIds] = JsonSerializer.Serialize(ids),
    };

    private static string Sentence(string whyNot) => char.ToUpperInvariant(whyNot[0]) + whyNot[1..] + ".";
}
