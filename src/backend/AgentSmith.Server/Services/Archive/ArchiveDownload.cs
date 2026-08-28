using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;

namespace AgentSmith.Server.Services.Archive;

/// <summary>
/// 2026-08-28-3793: the archive as a response body, written as it is produced.
/// <para>
/// Nothing buffers it: the writer streams a table at a time straight onto the response, so
/// the server never holds the whole file and the download starts before the last table has
/// been read. The price is that a failure halfway cannot change a status code that is
/// already sent — the body simply stops, and a zip whose directory never arrived will not
/// open. That is the better failure: a truncated file announces itself, where a plausible
/// archive missing its last tables would not.
/// </para>
/// </summary>
public sealed class ArchiveDownload(
    IDataArchiveWriter writer,
    SynchronousResponseWrites writes,
    TimeProvider clock,
    ILogger<ArchiveDownload> logger)
{
    private const string ArchiveContentType = "application/zip";

    public IResult Stream(
        AgentSmithDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        // A zip closes each entry with a synchronous write, which Kestrel refuses unless the
        // request says otherwise — and after the headers are out that refusal arrives as a
        // truncated body, not as an error.
        writes.Allow(context);
        return Results.Stream(
            stream => WriteAsync(db, stream, cancellationToken), ArchiveContentType, FileName());
    }

    private async Task WriteAsync(AgentSmithDbContext db, Stream destination, CancellationToken ct)
    {
        var manifest = await writer.WriteAsync(db, destination, ct);
        logger.LogInformation(
            "Streamed a data archive at schema {Schema}: {Tables} tables, {Rows} rows.",
            manifest.SchemaHead, manifest.Tables.Count, manifest.Tables.Sum(t => t.Rows));
    }

    private string FileName() =>
        $"agentsmith-archive-{clock.GetUtcNow():yyyyMMdd-HHmmss}Z.zip";
}
