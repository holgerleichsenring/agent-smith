using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// 2026-10-29-6bd3a: writes <see cref="LegacyTriggerFold"/> into the stored configuration once, so the
/// studio shows the routing that runs and no stored project carries a trigger block any more.
/// <para>
/// Nothing waits for it: every load folds the same blocks before it builds a trigger, so the write
/// changes what is shown, never what runs. That is why it runs after the server listens, and why a
/// stale version — another replica or a studio save got there first — is skipped rather than
/// reported: the next start, or the next load, still sees the block.
/// </para>
/// <para>
/// Trackers are saved before projects, and a project's block is cleared only once its tracker
/// holds what was moved onto it; the other order would lose the block's values on a stale tracker.
/// A document the fold did not change is not re-saved: every save bumps a version, and a standing
/// unmoved-ticket record is dropped with it.
/// </para>
/// </summary>
internal sealed class LegacyTriggerMigration(
    IConfigDocumentStore documents,
    ConfigDocumentAssembler assembler,
    IStartupFindings findings,
    ILogger<LegacyTriggerMigration> logger)
{
    private const string Actor = "trigger-block-migration";
    private const string Note = "per-project trigger block moved onto the project's resolution and its tracker";

    /// <summary>How many documents were rewritten. Zero is the steady state.</summary>
    public int Run()
    {
        try
        {
            return Rewrite();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The stored trigger blocks could not be moved onto their trackers");
            findings.Record(Unmigrated(ex));
            return 0;
        }
    }

    private int Rewrite()
    {
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
            if (!TrySave(tracker, versions)) unsaved.Add(tracker.Id);

        var projects = Docs(raw, ConfigDocTypes.Project).ToDictionary(d => d.Id);

        var rewritten = 0;
        foreach (var name in carrying)
        {
            if (unsaved.Contains(raw.Projects[name].Tracker)) continue;
            if (TrySave(projects[name], versions)) rewritten++;
        }
        logger.LogInformation(
            "Moved the trigger blocks of {Count} project(s) onto their resolution and tracker", rewritten);
        return rewritten;
    }

    private bool TrySave(DecomposedConfigDoc doc, IReadOnlyDictionary<(string, string), int> versions)
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
                "Config document {Type}/{Id} changed while its trigger block was being moved; left for the next start",
                doc.Type, doc.Id);
            return false;
        }
    }

    private IEnumerable<DecomposedConfigDoc> Docs(RawAgentSmithConfig raw, string type) =>
        assembler.Decompose(raw).Where(d => d.Type == type);

    private static bool HasBlock(RawProjectEntry p) =>
        p.LegacyJiraTrigger is not null || p.LegacyGithubTrigger is not null
        || p.LegacyGitlabTrigger is not null || p.LegacyAzuredevopsTrigger is not null;

    private static StartupFinding Unmigrated(Exception ex) => new(
        StartupSubsystems.Configuration,
        StartupFindingSeverity.Advisory,
        "The stored per-project trigger blocks could not be moved onto their projects and trackers. "
        + "They are still folded at every load, so routing is unaffected; the studio shows the tracker "
        + $"fields without them until a later start succeeds. Cause: {ex.Message}",
        Field: "projects");
}
