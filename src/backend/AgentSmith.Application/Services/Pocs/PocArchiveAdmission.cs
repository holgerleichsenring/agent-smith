namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: the upload's verdict on an archive from its central directory (and the root Dockerfile):
/// the refusal and the check that made it, or the files and the Dockerfile's facts.</summary>
public sealed record PocArchiveAdmission(
    string? Refusal, string? Check, IReadOnlyList<PocArchiveEntry> Entries, PocDockerfile? Dockerfile)
{
    public bool IsRefused => Refusal is not null;

    public long UnpackedBytes => Entries.Sum(e => e.Size);

    internal static PocArchiveAdmission Refused(PocRuleRefusal refusal) => new(refusal.Reason, refusal.Check, [], null);
}
