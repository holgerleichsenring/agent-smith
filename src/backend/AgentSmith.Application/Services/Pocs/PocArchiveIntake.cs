namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: the intake's verdict on an archive after inflating every file: the refusal and the check
/// that made it, or the manifest and the Dockerfile's facts.</summary>
public sealed record PocArchiveIntake(string? Refusal, string? Check, PocManifest? Manifest, PocDockerfile? Dockerfile)
{
    public bool IsRefused => Refusal is not null;

    internal static PocArchiveIntake Refused(PocRuleRefusal refusal) => new(refusal.Reason, refusal.Check, null, null);
}
