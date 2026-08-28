using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Server.Models;
using Microsoft.EntityFrameworkCore;
using static AgentSmith.Server.Services.Config.ConfigStudioWriteGuard;

namespace AgentSmith.Server.Services.Archive;

/// <summary>
/// 2026-08-28-3793: takes an uploaded archive and writes it into this server's own
/// database, then makes the server serve what it now holds.
/// <para>
/// The archive carries the config store, and the running server holds its configuration in
/// memory behind the epoch signal — so a restore that skipped the reload would hand back an
/// installation still behaving like the database it just replaced, until a restart nobody
/// was told to perform. The reload runs through the same ceremony the YAML import uses, and
/// only on success: a refused restore changes nothing, so it signals nothing.
/// </para>
/// </summary>
public sealed class ArchiveRestore(
    IDataArchiveReader reader,
    ArchiveUploadSpool spool,
    IConfigStore store,
    IConfigReloadSignal reload,
    ISystemEventPublisher events,
    ILogger<ArchiveRestore> logger)
{
    public async Task<IResult> RestoreAsync(
        AgentSmithDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        await using var upload = await spool.SpoolAsync(context, cancellationToken);
        try
        {
            var report = await reader.ReadAsync(db, upload, cancellationToken);
            return await GuardSignalingAsync(context, reload, events, () => Reloaded(report));
        }
        catch (DataArchiveException ex)
        {
            logger.LogWarning(ex, "A restore was refused");
            return Refused(ex.Message);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "A restore collided with a row this database already held");
            return Refused(
                "This database already holds a row the archive also carries, so the restore was "
                + $"rolled back and nothing was written. Cause: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    private IResult Reloaded(DataArchiveImportReport report)
    {
        store.Load();
        logger.LogInformation(
            "Restored a data archive at schema {Schema}: {Rows} rows across {Tables} tables.",
            report.Manifest.SchemaHead, report.TotalRows, report.Written.Count);
        return Results.Ok(new ArchiveRestoreResponse(
            report.Manifest.SchemaHead, report.Written, report.TotalRows));
    }

    private static IResult Refused(string cause) =>
        Results.Json(new ArchiveRefusalResponse(cause), statusCode: StatusCodes.Status409Conflict);
}
