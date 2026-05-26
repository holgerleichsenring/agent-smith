using AgentSmith.Server.Api.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;

namespace AgentSmith.Server.Api;

/// <summary>
/// p0169a: /api/jobs* endpoint group. Dashboard's Job-Viewer
/// (src/dashboard/src/app/page.tsx + jobs/[id]/page.tsx) is the sole
/// consumer today. CORS is wired in Program.cs.
/// </summary>
public static class JobsEndpoints
{
    public const string CorsPolicy = "Dashboard";
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    public static IEndpointRouteBuilder MapJobsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs").WithTags("Jobs").RequireCors(CorsPolicy);

        group.MapGet("/", (RunMetaReader reader, int? page, int? pageSize) =>
        {
            var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
            var pageNumber = Math.Max(1, page ?? 1);
            var all = reader.ListAll();
            var paged = all.Skip((pageNumber - 1) * size).Take(size).Select(ToResponse).ToList();
            return Results.Ok(new JobListResponse(paged, all.Count, pageNumber, size));
        }).WithName("ListJobs");

        group.MapGet("/{id}", (string id, RunMetaReader reader, RunArtefactLister lister) =>
        {
            var meta = reader.Read(id);
            if (meta is null) return Results.NotFound();
            var dir = reader.GetRunDir(id);
            var artefacts = dir is not null ? lister.List(dir) : Array.Empty<RunArtefactResponse>();
            return Results.Ok(new JobDetailResponse(ToResponse(meta), artefacts));
        }).WithName("GetJob");

        group.MapGet("/{id}/files/{**path}", (string id, string path, RunMetaReader reader) =>
        {
            var dir = reader.GetRunDir(id);
            if (dir is null) return Results.NotFound();
            if (!PathTraversalGuard.TryResolveWithin(dir, path, out var fullPath))
                return Results.BadRequest(new { error = "invalid_path" });
            var bytes = File.ReadAllBytes(fullPath);
            var contentType = GuessContentType(fullPath);
            return Results.File(bytes, contentType);
        }).WithName("GetJobFile");

        return app;
    }

    private static RunMetaResponse ToResponse(RunMetaFrontmatter m) => new(
        RunId: m.RunId,
        PipelineName: m.PipelineName ?? "unknown",
        Status: m.Status ?? "unknown",
        StartedAt: m.StartedAt,
        DurationSeconds: m.DurationSeconds,
        RepoMode: m.RepoMode ?? "unknown",
        SandboxCount: m.SandboxCount,
        Repos: m.Repos,
        Ticket: m.Ticket,
        Type: m.Type);

    private static string GuessContentType(string filename) =>
        Path.GetExtension(filename).ToLowerInvariant() switch
        {
            ".md" => "text/markdown",
            ".json" => "application/json",
            ".yaml" or ".yml" => "application/yaml",
            _ => "text/plain",
        };
}
