namespace AgentSmith.Server.Services.Archive;

/// <summary>
/// 2026-08-28-3793: lands an uploaded archive in a temporary file and hands back a seekable
/// stream over it.
/// <para>
/// A zip is read from its END — the directory sits there — so a reader that must consult
/// the manifest before it writes anything cannot work off a request body, which arrives
/// once and forwards only. The file carries <see cref="FileOptions.DeleteOnClose"/>, so it
/// is gone when the stream is disposed whether the import succeeded, refused or threw.
/// </para>
/// </summary>
public sealed class ArchiveUploadSpool(ArchiveUploadCeiling ceiling, ILogger<ArchiveUploadSpool> logger)
{
    public async Task<FileStream> SpoolAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ceiling.Raise(context);
        var file = TemporaryFile();
        try
        {
            await context.Request.Body.CopyToAsync(file, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "An archive upload could not be spooled to a temporary file");
            await file.DisposeAsync();
            throw;
        }

        file.Position = 0;
        logger.LogInformation("Spooled an archive upload of {Bytes} byte(s).", file.Length);
        return file;
    }

    private static FileStream TemporaryFile() => new(
        Path.Combine(Path.GetTempPath(), $"agentsmith-archive-{Path.GetRandomFileName()}"),
        new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Options = FileOptions.DeleteOnClose | FileOptions.Asynchronous,
        });
}
