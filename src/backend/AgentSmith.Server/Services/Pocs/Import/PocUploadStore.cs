using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>
/// 2026-11-23-933db: a person's PoC uploads in the ReferenceFile byte store, under their owner-keyed session key. A new
/// upload replaces their earlier un-imported ones (no PocRevision names them), so stored bytes stay bounded at one
/// pending upload per person without a limit of their own. The archive is stored once, whole; the upload id is the set
/// id the store mints.
/// </summary>
internal sealed class PocUploadStore(ReferenceSetRepository files, PocImportRepository imports)
{
    private const string ZipMediaType = "application/zip";
    private const string FallbackName = "upload.zip";
    private const int MaxNameChars = 200; // not a limit: keeps the stored name inside the ReferenceFile path column

    /// <summary>Stores <paramref name="archive"/> for the session and answers its upload id.</summary>
    public async Task<string> ReplaceAsync(string session, string fileName, byte[] archive, CancellationToken ct)
    {
        var imported = await imports.ImportedUploadIdsAsync(session, ct);
        await files.DeleteSetsExceptAsync(session, imported, ct);
        var stored = await files.AddAsync(session,
            [new ReferenceFile { RelativePath = StoredName(fileName), MediaType = ZipMediaType, Content = archive }], ct);
        return stored.SetId;
    }

    /// <summary>The session's upload: its file name and bytes, or null when it is not the session's or is gone.</summary>
    public async Task<(string FileName, byte[] Archive)?> ReadAsync(string session, string uploadId, CancellationToken ct) =>
        (await files.FilesAsync(session, uploadId, ct)) is [var file, ..] ? (file.Path, file.Content) : null;

    private static string StoredName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        return name.Length is 0 or > MaxNameChars ? FallbackName : name;
    }
}
