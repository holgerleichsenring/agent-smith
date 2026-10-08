using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Contracts.Sweep;

/// <summary>2026-10-08-10b0: a comment on an open pull request, with the host's own id for ordering
/// two comments of one second and the author fields the host's trust check needs.</summary>
public sealed record PrSweepComment(string PrNumber, string Id, DateTimeOffset CreatedAt, string Body, PrCommentAuthor Author);
