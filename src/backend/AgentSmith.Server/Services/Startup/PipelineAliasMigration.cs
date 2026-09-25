using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// 2026-09-25-e5b1: moves an installation's stored configuration off the four coding preset
/// names p0393 retired into aliases of <c>code</c>, ONCE, before the server answers a request.
/// <para>
/// It is not a tidy-up. With the alias map deleted a stored <c>fix-bug</c> stops resolving, and a
/// pipeline name that stops resolving is not merely unknown: the validator rejects it in
/// <c>pipeline_triggers</c> and in a project's <c>pipelines</c> list as BLOCKING, and every
/// per-preset classification then misses its map — the run would be typed as a conversation,
/// sized as a sandbox that builds nothing, and excluded from the clarification park. So the
/// configuration moves first and the map goes second.
/// </para>
/// <para>
/// A document naming none of them is NOT re-saved, and that is the whole reason the rewrite
/// answers null instead of always returning a document. A save bumps the doc's version, and a
/// standing <c>UnmovedTicket</c> record is dropped once the configuration it was recorded against
/// has moved. Every document that DOES carry a retired name pays that once: those refusals are
/// re-recorded by the next run that hits them again, at the cost of one repeated attempt.
/// </para>
/// <para>
/// It runs beside <c>RoleMappingMigration</c> and reports the same way — a store it could not
/// reach is a finding, never a dead process, because the configuration it failed to move is
/// exactly the configuration the operator needs the server up to fix.
/// </para>
/// </summary>
internal sealed class PipelineAliasMigration(
    IConfigDocumentStore documents,
    ConfigDocumentAssembler assembler,
    IStartupFindings findings,
    ILogger<PipelineAliasMigration> logger)
{
    private const string Actor = "preset-alias-migration";
    private const string Note = "retired preset name rewritten to the preset that replaced it";

    /// <summary>How many documents were rewritten. Zero is the steady state.</summary>
    public int Run()
    {
        try
        {
            return Rewrite();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The stored configuration could not be moved off the retired "
                + "preset names");
            findings.Record(Unmigrated(ex));
            return 0;
        }
    }

    private int Rewrite()
    {
        var rewritten = 0;
        foreach (var row in documents.LoadAll())
        {
            if (PipelineAliasRewrite.Apply(row.Type, row.Doc) is not { } doc) continue;
            documents.Save(new ConfigDocWrite(
                row.Type, row.Id, doc, row.Version,
                assembler.EdgesFor(row.Type, doc), Actor, Note));
            logger.LogInformation(
                "Rewrote a retired preset name in config document {Type}/{Id}", row.Type, row.Id);
            rewritten++;
        }

        if (rewritten > 0)
            logger.LogInformation(
                "Moved {Count} config document(s) off the retired preset names. Their standing "
                + "unmoved-ticket records were dropped with the version bump and will be "
                + "re-recorded by the next run that meets the same refusal", rewritten);
        return rewritten;
    }

    private static StartupFinding Unmigrated(Exception ex) => new(
        StartupSubsystems.Configuration,
        StartupFindingSeverity.Advisory,
        "The stored configuration could not be moved off the retired preset names "
        + $"({string.Join(", ", RetiredPipelineNames.Replacements.Keys)}). Any routing rule "
        + "still naming one now names a pipeline that does not exist, and a ticket routed to "
        + $"it will fail when it starts. Cause: {ex.Message}",
        Field: "pipeline_triggers");
}
