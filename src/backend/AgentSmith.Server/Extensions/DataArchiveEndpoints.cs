using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Archive;
using Microsoft.AspNetCore.Mvc;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-08-28-3793: the whole database, out and back in, for an operator with a browser and
/// no shell on the machine the database runs on. The CLI verb stays the reliable path — it
/// works on an installation whose server will not start — and this is the ordinary one.
/// <para>
/// All three routes state <c>archive.*</c> rather than the configuration grant: an archive
/// carries ticket text, prompts, artifacts and the config store's secrets in clear, so
/// whoever may take one may read everything this installation has ever done.
/// </para>
/// </summary>
internal static class DataArchiveEndpoints
{
    internal static WebApplication MapDataArchiveEndpoints(this WebApplication app)
    {
        // What the file WOULD carry, read before it is asked for — an archive is the whole
        // installation in clear, and the download is a poor moment to learn that.
        app.MapGet("/api/archive/preview",
            ([FromServices] ArchivePreviewReader preview, [FromServices] AgentSmithDbContext db,
                CancellationToken cancellationToken) => preview.ReadAsync(db, cancellationToken))
           .Needs(Permissions.ArchiveExport);

        app.MapGet("/api/archive/export",
            ([FromServices] ArchiveDownload download, [FromServices] AgentSmithDbContext db,
                HttpContext context, CancellationToken cancellationToken) =>
                download.Stream(db, context, cancellationToken))
           .Needs(Permissions.ArchiveExport);

        app.MapPost("/api/archive/import",
            ([FromServices] ArchiveRestore restore, [FromServices] AgentSmithDbContext db,
                HttpContext context, CancellationToken cancellationToken) =>
                restore.RestoreAsync(db, context, cancellationToken))
           .Needs(Permissions.ArchiveImport);

        return app;
    }
}
