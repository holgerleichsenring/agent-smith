namespace AgentSmith.Server.Security;

/// <summary>
/// p0503a: why this surface answers without a token. The framework's
/// <c>AllowAnonymousAttribute</c> says THAT a route is anonymous and implements
/// <c>IAllowAnonymous</c> only — never <c>IAuthorizeData</c> — so it is genuinely inert;
/// this rides alongside it and says WHY, because a reason a reader can check is the
/// difference between a decision and an omission.
/// </summary>
internal sealed record AnonymousRoute(string Reason);
