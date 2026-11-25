using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Migration.Services.ConfigDocuments;

/// <summary>
/// 2026-11-25-9992d: moves the installation-wide <c>registries</c> singleton onto the projects, ONCE. The feeds live
/// under <c>projects.&lt;name&gt;.registries</c> now and the singleton is no longer assembled, so the hosts and token
/// references an operator stored there would otherwise stop reaching any run. Every stored project that has no feeds
/// of its own gets a copy, as a new version of its document; a project with its own list keeps it. Then the singleton
/// is deleted, which is what makes a second run find nothing. A failure is logged and left unrecorded: the next
/// migrate tries again, and a project already given its copy keeps it.
/// </summary>
public sealed class ProjectRegistriesMigration(
    ConfigDocumentAssembler assembler, ILogger<ProjectRegistriesMigration> logger) : IDataMigration
{
    private const string Actor = "project-registries-migration";
    private const string Note = "installation-wide registries copied onto the project (2026-11-25-9992d)";
    private const string Field = "Registries";

    public string Id => "2026-11-25-9992d-project-registries";

    public string? ShippedWith => null;

    public Task<bool> RunAsync(AgentSmithDbContext db, CancellationToken ct) =>
        Task.FromResult(TryMove(new ContextConfigDocumentStore(db)));

    /// <summary>Move that logs a failure instead of throwing; false when it did not complete.</summary>
    public bool TryMove(IConfigDocumentStore documents)
    {
        try
        {
            return Move(documents) is not null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The stored registries could not be moved onto the projects");
            return false;
        }
    }

    /// <summary>How many project documents received the feeds; zero once the singleton is gone. Null when feeds are
    /// stored but no project is: the singleton is kept, so nothing is lost, and the migration is not recorded.</summary>
    public int? Move(IConfigDocumentStore documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var rows = documents.LoadAll();
        var singleton = rows.FirstOrDefault(r => r.Type == ConfigDocTypes.Registries);
        if (singleton is null) return 0;
        var feeds = JsonNode.Parse(singleton.Doc) as JsonArray ?? [];
        var projects = rows.Where(r => r.Type == ConfigDocTypes.Project).ToList();
        if (feeds.Count > 0 && projects.Count == 0)
        {
            logger.LogWarning(
                "{Feeds} stored registry entr(ies) were kept: no project is stored to receive them. Create a project "
                + "and migrate again, or re-enter the feeds on a project", feeds.Count);
            return null;
        }
        // Every project now holds feeds — the copy or its own list — so the singleton has nothing left to give.
        var moved = feeds.Count == 0 ? 0 : projects.Count(project => TryGive(documents, project, feeds));
        documents.Delete(ConfigDocTypes.Registries, singleton.Id, Actor);
        logger.LogInformation(
            "Moved {Feeds} installation-wide registry entr(ies) onto {Projects} project(s) and removed the singleton",
            feeds.Count, moved);
        return moved;
    }

    private bool TryGive(IConfigDocumentStore documents, ConfigDocRow project, JsonArray feeds)
    {
        if (JsonNode.Parse(project.Doc) is not JsonObject doc || HasOwn(doc)) return false;
        foreach (var key in doc.Select(p => p.Key).Where(IsField).ToList()) doc.Remove(key);
        doc[Field] = feeds.DeepClone();
        var json = doc.ToJsonString();
        documents.Save(new ConfigDocWrite(
            project.Type, project.Id, json, project.Version, assembler.EdgesFor(project.Type, json), Actor, Note));
        return true;
    }

    // The store reads property names case-insensitively, so any spelling of the field is the project's own list.
    private static bool HasOwn(JsonObject doc) =>
        doc.Any(p => IsField(p.Key) && p.Value is JsonArray { Count: > 0 });

    private static bool IsField(string key) => string.Equals(key, Field, StringComparison.OrdinalIgnoreCase);
}
