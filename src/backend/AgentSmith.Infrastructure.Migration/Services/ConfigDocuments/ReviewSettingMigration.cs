using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Migration.Services.ConfigDocuments;

/// <summary>
/// 2026-11-25-9992i: moves every stored project off merge.when, merge.series_when, merge.respect_branch_protection and
/// merge.refresh_open_pull_requests onto the one project setting review, ONCE (<see cref="ReviewSettingMapping"/>). The
/// keys are no longer read, so a project left unmoved would silently fall back to review: end. A project whose merge block
/// names none of them is not re-saved. A review already stored is never overwritten, readable or not: the old keys are
/// dropped and the project is named in the log. A project whose standalone tickets now behave otherwise is named in the log
/// with how. A failure is logged and left unrecorded: the next migrate tries again.
/// </summary>
public sealed class ReviewSettingMigration(
    ConfigDocumentAssembler assembler, ILogger<ReviewSettingMigration> logger) : IDataMigration
{
    private const string Actor = "review-setting-migration";
    private const string Note = "merge decision keys rewritten to review (2026-11-25-9992i)";

    public string Id => "2026-11-25-9992i-review-setting";

    public string? ShippedWith => null;

    public Task<bool> RunAsync(AgentSmithDbContext db, CancellationToken ct) =>
        Task.FromResult(TryMove(new ContextConfigDocumentStore(db)));

    /// <summary>Move that logs a failure instead of throwing; false when it did not complete.</summary>
    public bool TryMove(IConfigDocumentStore documents)
    {
        try
        {
            Move(documents);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The stored projects could not be moved onto the review setting");
            return false;
        }
    }

    /// <summary>How many project documents were rewritten; zero once every one is moved.</summary>
    public int Move(IConfigDocumentStore documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var moved = documents.LoadAll().Where(r => r.Type == ConfigDocTypes.Project).Count(row => TryRewrite(documents, row));
        logger.LogInformation("Moved {Projects} stored project(s) onto the review setting", moved);
        return moved;
    }

    private bool TryRewrite(IConfigDocumentStore documents, ConfigDocRow row)
    {
        if (JsonNode.Parse(row.Doc) is not JsonObject doc || Field(doc, "Merge") is not JsonObject merge
            || !ReviewSettingMapping.Names(merge)) return false;
        var mapped = ReviewSettingMapping.Of(merge);
        if (Own(doc) is { } stored)
            logger.LogWarning("Project {Project} keeps its stored review '{Stored}'; its retired merge keys (mapping to {Mapped}) "
                + "were dropped", row.Id, stored, ReviewModeNames.Of(mapped));
        else Write(doc, merge, mapped, row.Id);
        ReviewSettingMapping.Drop(merge);
        var json = doc.ToJsonString();
        documents.Save(new ConfigDocWrite(row.Type, row.Id, json, row.Version, assembler.EdgesFor(row.Type, json), Actor, Note));
        return true;
    }

    // The mapped review onto a project that stored none, with what its standalone tickets now do otherwise.
    private void Write(JsonObject doc, JsonObject merge, ReviewMode review, string project)
    {
        foreach (var change in ReviewSettingMapping.Changes(merge, review))
            logger.LogWarning("Project {Project} now runs under review: {Review}: {Change}", project, ReviewModeNames.Of(review), change);
        foreach (var key in doc.Select(p => p.Key).Where(k => Same(k, "Review")).ToList()) doc.Remove(key);
        doc["Review"] = ReviewModeNames.Of(review);
    }

    // A review the project already stores, as written — readable or not, it is the operator's and is kept.
    private static string? Own(JsonObject doc) => Field(doc, "Review") is JsonValue v && v.ToString() is { Length: > 0 } written
        && !string.IsNullOrWhiteSpace(written) ? written : null;

    private static JsonNode? Field(JsonObject doc, string key) => doc.FirstOrDefault(p => Same(p.Key, key)).Value;

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
