namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: the MACHINE half of work-spec persistence. Content lives in git; only
/// what nobody reads by hand lives here — which repo carries the spec (so a
/// changed scope on a later run cannot leave the pointer aiming at something
/// absent), the sha of the last revision THIS system wrote (so a reviewer's edit
/// is recognisable), and the per-ticket hand-back counters.
/// </summary>
public interface IWorkSpecPointerStore
{
    Task<WorkSpecPointer?> GetAsync(string project, string key, CancellationToken cancellationToken);

    Task SaveAsync(string project, WorkSpecPointer pointer, CancellationToken cancellationToken);
}
