namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: the MACHINE state of a work spec. Persistence splits by reader —
/// content lives in git on the ticket branch (diff, blame, history and the PR
/// review are the UI), and only what nobody reads by hand lives in the database:
/// which repo carries the spec, the sha of the last revision this system wrote,
/// and the per-ticket hand-back counters that end a non-progressing loop.
/// </summary>
public sealed record WorkSpecPointer(
    string Key,
    string CarryingRepo,
    string RevisionSha,
    int RevisionNumber,
    WorkSpecHandbackCase LastHandbackCase = WorkSpecHandbackCase.None,
    int RepeatedHandbackCount = 0,
    string? HandbackSourceSha = null);
