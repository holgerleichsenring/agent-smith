namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-10-13-9a59d: where the act an attempt served was made — its kind (<see cref="AttemptSourceKinds"/>),
/// the pull request's url for a review or command, the host's id of the comment or note, and a detail
/// (the status a ticket was moved into). Data, not a link: a view links or quotes from it. Where a host
/// gives no id, the url and the act's own time stand in.
/// </summary>
public sealed record AttemptSource(string Kind, string? Url = null, string? Id = null, string? Detail = null);
