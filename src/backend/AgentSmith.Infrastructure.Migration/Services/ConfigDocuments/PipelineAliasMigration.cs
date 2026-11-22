using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Migration.Services.ConfigDocuments;

/// <summary>
/// 2026-09-25-e5b1: moves an installation's stored configuration off the four coding preset
/// names p0393 retired into aliases of <c>code</c>, ONCE.
/// <para>
/// It is not a tidy-up. With the alias map deleted a stored <c>fix-bug</c> stops resolving, and a
/// pipeline name that stops resolving is not merely unknown: the validator rejects it in
/// <c>pipeline_triggers</c> and in a project's <c>pipelines</c> list as BLOCKING. So the
/// configuration moves first and the map goes second.
/// </para>
/// <para>
/// A document naming none of them is NOT re-saved, and that is the whole reason the rewrite
/// answers null instead of always returning a document. A save bumps the doc's version, and a
/// standing <c>UnmovedTicket</c> record is dropped once the configuration it was recorded against
/// has moved.
/// </para>
/// <para>
/// 2026-11-22-747da: a data migration of `database migrate`, no longer a step of the server's start. A failure is
/// logged and left unrecorded, so the next migrate tries again; it never fails the migrate, because the configuration
/// it could not move is the configuration the operator needs the server up to fix.
/// </para>
/// </summary>
public sealed class PipelineAliasMigration(
    ConfigDocumentAssembler assembler, ILogger<PipelineAliasMigration> logger) : IDataMigration
{
    private const string Actor = "preset-alias-migration";
    private const string Note = "retired preset name rewritten to the preset that replaced it";

    public string Id => "2026-09-25-e5b1-preset-aliases";

    public string? ShippedWith => null;

    public Task<bool> RunAsync(AgentSmithDbContext db, CancellationToken ct) =>
        Task.FromResult(TryRewrite(new ContextConfigDocumentStore(db)));

    /// <summary>Rewrite that logs a failure instead of throwing; false when it did not complete.</summary>
    public bool TryRewrite(IConfigDocumentStore documents)
    {
        try
        {
            Rewrite(documents);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The stored configuration could not be moved off the retired preset names");
            return false;
        }
    }

    /// <summary>How many documents were rewritten. Zero is the steady state.</summary>
    public int Rewrite(IConfigDocumentStore documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
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
        return rewritten;
    }
}
