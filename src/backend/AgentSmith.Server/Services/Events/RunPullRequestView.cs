namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0350: one pull request a run opened, per repo. The snapshot used to carry a
/// single <c>PrUrl</c> (the FIRST repo whose row was "opened"), so a multi-repo
/// run that opened several PRs collapsed to one link and the others were
/// invisible on the run detail. This list surfaces EVERY opened PR — draft or
/// ready — so a 3-repo/2-PR run shows both.
/// </summary>
/// <param name="Repo">The repository the PR was opened against.</param>
/// <param name="Url">The browsable PR web URL.</param>
/// <param name="Status">The recorded PR status ("opened" for a real PR, including drafts).</param>
/// <param name="IsDraft">
/// True when the PR was opened as a draft (a red/keystone-unsatisfied run opens
/// drafts so the work is preserved for review). Currently derived from the run's
/// terminal status, not persisted per-PR.
/// </param>
public sealed record RunPullRequestView(string Repo, string Url, string Status, bool IsDraft);
