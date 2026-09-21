namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-09-21-5c17: the one quota key a run does not fit under, kept apart from the sentences
/// it is told in. The refusal now travels to an operator surface — a queued run's row, the
/// manual launch door's response body, a database column — so what it CARRIES names the
/// resource, the bound and the usage and nothing of the estate. The quota object and its
/// namespace are what a cluster operator with three quotas in one namespace needs in order to
/// find the right one, so the probe logs them rather than losing them.
/// </summary>
internal sealed record QuotaShortfall(string? QuotaName, string Resource, string Hard, string Used)
{
    /// <summary>The operator-facing refusal: the bound and its usage lead.</summary>
    public string Reason => $"{Resource} is at its limit: hard {Hard}, used {Used} — waiting for room.";
}
