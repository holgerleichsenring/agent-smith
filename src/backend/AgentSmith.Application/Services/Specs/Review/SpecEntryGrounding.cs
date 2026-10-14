using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762n: re-grounds the spec SelectSpec is entering, before its start heads are recorded.</summary>
public interface ISpecEntryGrounding
{
    Task<SpecEntryGround> GroundAsync(PipelineContext pipeline, SpecSet set, DerivedSpec spec, CancellationToken ct);
}

/// <summary>2026-10-13-8762n: the spec to enter and what SelectSpec's step line adds — empty, or the stale
/// citations (count and the first three) and a failed publish.</summary>
public sealed record SpecEntryGround(DerivedSpec Spec, string StepNote);

/// <summary>
/// 2026-10-13-8762n: the moved citations of the entered spec become ONE visible set revision, published
/// through the series' one mid-run publisher with the approval kept; nothing moved publishes nothing. A
/// revision the branch did not take does NOT fail the spec (contract default, pending operator
/// ratification): the handed-in set is put back, the spec runs on its original text — the text the branch
/// states — and each moved citation is listed stale with where its text was found.
/// </summary>
public sealed class SpecEntryGrounding(
    SpecCitationRegrounder regrounder, SpecReviewRevision revision, SpecDraftReader reader,
    ILogger<SpecEntryGrounding> logger) : ISpecEntryGrounding
{
    public const int NamedInCause = 5;
    public const int NamedInNote = 3;

    public async Task<SpecEntryGround> GroundAsync(PipelineContext pipeline, SpecSet set, DerivedSpec spec, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var found = await regrounder.RegroundAsync(pipeline, set, spec, ct);
        var stale = found.Stale.ToList();
        var (entered, failure) = found.Moves.Count == 0 ? (spec, null) : await PublishAsync(pipeline, set, spec, found, ct);
        if (failure is not null)
            stale.AddRange(found.Moves.Select(m => $"{m.Old}: its text is now at {m.New}; the re-ground revision was not published"));
        pipeline.Set<IReadOnlyList<string>>(ContextKeys.StaleCitations, stale);
        return new SpecEntryGround(entered, Note(stale, failure));
    }

    /// <summary>The revision's cause: each old and new citation, at most <see cref="NamedInCause"/> named.</summary>
    public static string Cause(string specId, IReadOnlyList<SpecCitationMove> moves) =>
        $"{SpecRevisionCause.CitationsRegrounded} {specId}: "
        + string.Join("; ", moves.Take(NamedInCause).Select(m => m.Old + Tools.HandForwardToolHost.Arrow + m.New))
        + (moves.Count > NamedInCause ? $" and {moves.Count - NamedInCause} more" : string.Empty);

    private async Task<(DerivedSpec Spec, string? Failure)> PublishAsync(
        PipelineContext pipeline, SpecSet set, DerivedSpec spec, SpecReground found, CancellationToken ct)
    {
        var draft = reader.Read(found.Yaml) with { Repos = spec.Draft.Repos };
        var cause = Cause(spec.SpecId, found.Moves);
        var result = await revision.PublishAsync(pipeline, [draft], cause, ct);
        if (result.IsSuccess)
        {
            logger.LogInformation("Spec {SpecId} entered re-grounded: {Cause}", spec.SpecId, cause);
            var published = pipeline.Get<SpecSet>(ContextKeys.SpecSet).Specs.FirstOrDefault(s => s.SpecId == spec.SpecId);
            return (published ?? spec with { Draft = draft }, null);
        }
        logger.LogWarning("Spec {SpecId}: the re-ground revision was not published, the spec runs on its original text: {Message}",
            spec.SpecId, result.Message);
        pipeline.Set(ContextKeys.SpecSet, set);
        return (spec, result.Message);
    }

    private static string Note(IReadOnlyList<string> stale, string? failure) =>
        (stale.Count == 0 ? string.Empty
            : $"; {stale.Count} stale citation(s): {string.Join("; ", stale.Take(NamedInNote))}"
              + (stale.Count > NamedInNote ? $" and {stale.Count - NamedInNote} more" : string.Empty))
        + (failure is null ? string.Empty : $"; the re-ground revision was not published: {failure}");
}
