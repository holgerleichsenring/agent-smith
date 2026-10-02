namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// 2026-10-02-5f89b: the ordered steps of one draft check. The steps stop at the first failure,
/// so the last step of a failed report says what to fix — DNS, the token, its scope or the name.
/// </summary>
public sealed record DraftCheckReport(IReadOnlyList<DraftCheckStep> Steps)
{
    public bool Ok => Steps.Count > 0 && Steps.All(s => s.Ok);
}
