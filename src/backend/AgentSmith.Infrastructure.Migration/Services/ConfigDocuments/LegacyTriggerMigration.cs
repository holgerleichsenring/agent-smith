using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.Logging;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

namespace AgentSmith.Infrastructure.Migration.Services.ConfigDocuments;

/// <summary>
/// 2026-10-29-6bd3a: writes <see cref="LegacyTriggerFold"/> into the stored configuration once, so the
/// studio shows the routing that runs and no stored project carries a trigger block any more.
/// <para>
/// Nothing waits for it: every load folds the same blocks before it builds a trigger, so the write
/// changes what is shown, never what runs. That is why a stale version — a studio save got there
/// first — is skipped rather than reported: every load still sees the block.
/// </para>
/// <para>
/// Trackers are saved before projects, and a project's block is cleared only once its tracker
/// holds what was moved onto it; the other order would lose the block's values on a stale tracker.
/// A document the fold did not change is not re-saved: every save bumps a version, and a standing
/// unmoved-ticket record is dropped with it.
/// </para>
/// <para>
/// 2026-11-22-747da: a data migration of `database migrate`, no longer run after the server listens. It is recorded
/// once it ran, even when a stale version left a block: every load still folds what is left.
/// </para>
/// </summary>
public sealed class LegacyTriggerMigration(
    ConfigDocumentAssembler assembler, ILogger<LegacyTriggerMigration> logger) : IDataMigration
{
    private const string Actor = "trigger-block-migration";
    private const string Note = "per-project trigger block moved onto the project's resolution and its tracker";

    public string Id => "2026-10-29-6bd3a-trigger-blocks";

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
            logger.LogError(ex, "The stored trigger blocks could not be moved onto their trackers");
            return false;
        }
    }

    /// <summary>How many documents were rewritten. Zero is the steady state.</summary>
    public int Rewrite(IConfigDocumentStore documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var rows = documents.LoadAll();
        var raw = assembler.Assemble(rows);
        var carrying = raw.Projects.Where(p => HasBlock(p.Value)).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        if (carrying.Count == 0) return 0;

        // Both sides in today's shape: a stored document written before a field existed differs
        // from its re-serialisation without anything having changed.
        var before = Docs(raw, ConfigDocTypes.Tracker).ToDictionary(d => d.Id, d => d.Doc);
        new LegacyTriggerFold().Apply(raw);
        var versions = rows.ToDictionary(r => (r.Type, r.Id), r => r.Version);

        var unsaved = new HashSet<string>(ConfigNames.Comparer);
        foreach (var tracker in Docs(raw, ConfigDocTypes.Tracker).Where(d => before.GetValueOrDefault(d.Id) != d.Doc))
            if (!TrySave(documents, tracker, versions)) unsaved.Add(tracker.Id);

        var projects = Docs(raw, ConfigDocTypes.Project).ToDictionary(d => d.Id);

        var rewritten = 0;
        foreach (var name in carrying)
        {
            if (unsaved.Contains(raw.Projects[name].Tracker)) continue;
            if (TrySave(documents, projects[name], versions)) rewritten++;
        }
        logger.LogInformation(
            "Moved the trigger blocks of {Count} project(s) onto their resolution and tracker", rewritten);
        return rewritten;
    }

    private bool TrySave(
        IConfigDocumentStore documents, DecomposedConfigDoc doc, IReadOnlyDictionary<(string, string), int> versions)
    {
        try
        {
            documents.Save(new ConfigDocWrite(
                doc.Type, doc.Id, doc.Doc,
                versions.TryGetValue((doc.Type, doc.Id), out var version) ? version : null,
                doc.Edges, Actor, Note));
            return true;
        }
        catch (StaleConfigVersionException)
        {
            logger.LogInformation(
                "Config document {Type}/{Id} changed while its trigger block was being moved; left as it is; every load folds it",
                doc.Type, doc.Id);
            return false;
        }
    }

    private IEnumerable<DecomposedConfigDoc> Docs(RawAgentSmithConfig raw, string type) =>
        assembler.Decompose(raw).Where(d => d.Type == type);

    private static bool HasBlock(RawProjectEntry p) =>
        p.LegacyJiraTrigger is not null || p.LegacyGithubTrigger is not null
        || p.LegacyGitlabTrigger is not null || p.LegacyAzuredevopsTrigger is not null;
}
