using AgentSmith.Infrastructure.Persistence.Models;

namespace AgentSmith.Infrastructure.Persistence.Contracts;

/// <summary>
/// 2026-08-28-2af6: writes an archive back into an EMPTY store on any supported provider.
/// It refuses before it writes — a schema head whose name differs from the target's, or a
/// target that already holds rows — and verifies afterwards against the manifest.
/// </summary>
public interface IDataArchiveReader
{
    /// <summary>
    /// Reads the archive and writes it into <paramref name="db"/>, or throws
    /// <see cref="Domain.Exceptions.DataArchiveException"/> naming the check that stopped it.
    /// The stream must be seekable: a zip's directory sits at its end, and the manifest has
    /// to be read before anything is written.
    /// </summary>
    Task<DataArchiveImportReport> ReadAsync(
        AgentSmithDbContext db, Stream archive, CancellationToken cancellationToken = default);
}
