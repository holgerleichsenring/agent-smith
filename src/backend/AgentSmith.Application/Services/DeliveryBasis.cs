using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-09-13-5cdf: what a delivery diff is taken relative to — the run that is taking it,
/// and where that run sits in the cut.
/// <para>
/// The two travel together because the diff needs both and neither is a property of the
/// sandbox: the run start names the commit this run began at, and the parent stamp names
/// the rung the branch was cut from. Passing them as one value keeps every
/// <c>ForBranchAsync</c> call site reading the pipeline ONCE, which is what stops the
/// account from resolving a different base than the cut did.
/// </para>
/// </summary>
/// <param name="RunId">The run taking the diff, or null to leave the run-start rung out.</param>
/// <param name="ParentTicketId">The epic record this run is a slice of, or null.</param>
public sealed record DeliveryBasis(string? RunId, string? ParentTicketId)
{
    public static DeliveryBasis For(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return new DeliveryBasis(pipeline.RunId(), RunParentTicket.Of(pipeline));
    }

    /// <summary>A run with no place in a cut — every run before an epic was filed.</summary>
    public static DeliveryBasis OfRun(string? runId) => new(runId, ParentTicketId: null);
}
