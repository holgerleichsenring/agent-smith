using AgentSmith.Application.Services.Memory;
using AgentSmith.Contracts.Memory;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-10-fcd7d: a project's own memories for a person to check — newest first, each with its standing, record
/// and where it came from — and the one change a person makes to them: deleting one, which takes its ledger rows
/// along. Read under runs.read and deleted under runs.control on the project; 404 when no such project has a
/// config row.
/// </summary>
internal static class ProjectMemoryEndpoints
{
    internal static WebApplication MapProjectMemoryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/projects/{name}/memories", ListAsync).Needs(Permissions.RunsRead).InProject(ProjectFrom.Route("name"));
        app.MapDelete("/api/projects/{name}/memories/{slug}", DeleteAsync)
            .Needs(Permissions.RunsControl).InProject(ProjectFrom.Route("name"));
        return app;
    }

    // Internal so the endpoint tests drive the real store without a host.
    internal static async Task<IResult> ListAsync(
        string name, IProjectMemoryRepository memories, MemoryStandingReader standings, CancellationToken ct)
    {
        if (await memories.ResolveProjectAsync(name, ct) is not { } id) return Results.NotFound();
        var records = await memories.ListAsync(id, ct);
        var standing = await standings.OfKeyAsync(ProjectMemorySource.LedgerKey(id), ProjectMemorySource.Label, ct);
        return Results.Ok(records.Select(r =>
        {
            var s = standing.GetValueOrDefault(r.Slug) ?? MemoryStanding.None(r.Slug);
            return new ProjectMemoryView(r.Slug, r.Type, r.Description, r.Body, s.Label, s.Record,
                r.SourceKind, r.SourceId, r.SourceRef, r.Author, r.UpdatedAt);
        }).ToList());
    }

    internal static async Task<IResult> DeleteAsync(
        string name, string slug, IProjectMemoryRepository memories, CancellationToken ct)
    {
        if (await memories.ResolveProjectAsync(name, ct) is not { } id) return Results.NotFound();
        return await memories.DeleteAsync(id, slug, ct) ? Results.NoContent() : Results.NotFound();
    }
}

/// <summary>2026-11-10-fcd7d: one project memory as the panel shows it.</summary>
internal sealed record ProjectMemoryView(
    string Slug, string Type, string Description, string Body, string Standing, string Record,
    string SourceKind, string SourceId, string? SourceRef, string? Author, DateTimeOffset UpdatedAt);
