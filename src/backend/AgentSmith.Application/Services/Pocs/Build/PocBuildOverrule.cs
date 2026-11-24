using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>
/// 2026-11-23-933dd: an overrule of an image-stage block. The overrule run appends the blocking ids under 'image' and
/// clears the stored pipeline run, so a fresh run starts whose trivy_ignore carries both stages' ids; once that build
/// is pushed the overrule is published for the PoC's audit trail, naming the stage.
/// </summary>
public sealed class PocBuildOverrule(IPocScanStore scans, IPocBuildStore builds, PocOverruledPublisher overruled)
{
    /// <summary>Applies the overrule seeds to an image-blocked revision and answers the revision as it now reads, or why
    /// it cannot be overruled.</summary>
    public async Task<(PocScanSubject? Subject, string? Refused)> ApplyAsync(
        PocScanSubject subject, IReadOnlyList<string> ids, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (PocScanVerdict.FromJson(subject.ImageVerdictJson) is not { Overrulable: true })
            return (null, $"Revision {subject.Revision} of '{subject.PocName}' is blocked by a secret in its image, which cannot be overruled.");
        await scans.AppendOverruledAsync(subject.PocId, subject.Revision, PocScanStages.Image, ids, ct);
        await builds.RecordPipelineRunAsync(subject.PocId, subject.Revision, null, null, ct);
        return (await scans.ReadAsync(subject.PocId, subject.Revision, ct), null);
    }

    /// <summary>Publishes the image-stage overrule once its build was pushed.</summary>
    public async Task RecordAsync(PocBuildJob job, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);
        var seeds = job.Context.Seeds;
        var lifted = PocOverruledIds.FromJson(job.Subject.OverruledIdsJson).Of(PocScanStages.Image);
        if (!seeds.Overrule || job.Subject.BlockedStage != PocScanStages.Image || lifted.Count == 0) return;
        await overruled.PublishAsync(job.Context.Pipeline, job.Subject.PocId, job.Subject.Revision, PocScanStages.Image,
            seeds.Actor ?? string.Empty, seeds.OverruleReason ?? string.Empty, lifted, ct);
    }
}
