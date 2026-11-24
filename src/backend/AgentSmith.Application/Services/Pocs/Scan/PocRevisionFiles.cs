using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>2026-11-23-933dc: a validated revision's files from its stored upload, checked against its manifest — what
/// the scan reads and the commit writes. A purged upload, a missing manifest or a mismatch is a failure naming why.</summary>
public sealed class PocRevisionFiles(IPocIntakeStore uploads, PocManifestFiles manifests)
{
    public async Task<(IReadOnlyList<ReferenceSetFile> Files, string? Failure)> ReadAsync(PocScanSubject subject, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (subject.ManifestJson is not { } manifest)
            return ([], $"revision {subject.Revision} of '{subject.PocName}' has no validated manifest");
        if (await uploads.ArchiveAsync(subject.UploadSession, subject.UploadId, ct) is not { } archive)
            return ([], $"the upload of revision {subject.Revision} of '{subject.PocName}' is gone; import the ZIP again");
        return manifests.Read(archive, manifest);
    }
}
