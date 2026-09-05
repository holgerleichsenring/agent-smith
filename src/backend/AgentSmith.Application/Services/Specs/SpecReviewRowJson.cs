using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// The review's answer exactly as it arrives on the wire, before it is a typed row.
/// <para>
/// Reading happens here so <see cref="CriterionReview"/> stays a typed value with one
/// disposition, and so an unrecognised spelling has ONE place to be handled.
/// </para>
/// </summary>
internal sealed record SpecReviewRowJson
{
    public string? Criterion { get; init; }

    /// <summary>One of <c>decidable</c>, <c>prescribes_shape</c>, <c>no_observation_settles</c>,
    /// <c>already_true</c>.</summary>
    public string? Disposition { get; init; }

    /// <summary>The search the review ran, verbatim.</summary>
    public string? Observation { get; init; }

    /// <summary>What that search returned.</summary>
    public string? Output { get; init; }

    public string? Note { get; init; }

    /// <summary>The criterion this one should become.</summary>
    public string? Replacement { get; init; }

    public CriterionReview ToRow() =>
        new(Criterion ?? string.Empty, DispositionOf(), Observation, Output, Note, Replacement);

    /// <summary>
    /// Anything unrecognised is DECIDABLE — the pass-through floor. A misspelled disposition
    /// is the review failing to answer, and the safe reading of an answer nobody can
    /// interpret is the one that neither edits a contract nor parks a run.
    /// </summary>
    private SpecReviewDisposition DispositionOf() => Disposition?.Trim().ToLowerInvariant() switch
    {
        "prescribes_shape" or "prescribes-shape" or "shape" => SpecReviewDisposition.PrescribesShape,
        "no_observation_settles" or "no-observation-settles" or "unobservable"
            => SpecReviewDisposition.NoObservationSettles,
        "already_true" or "already-true" => SpecReviewDisposition.AlreadyTrue,
        _ => SpecReviewDisposition.Decidable,
    };
}
