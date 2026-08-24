namespace AgentSmith.Server.Models;

/// <summary>
/// p0503b: what a 403 says. ASP.NET's own forbid path writes an EMPTY body, which tells a
/// caller that it may not do this and nothing about what it would need — so an operator
/// debugging a role bundle is left guessing at a catalog they cannot see. This names the
/// permissions the caller lacked, and only those.
/// </summary>
public sealed record ForbiddenPermissionResponse(
    string Error,
    IReadOnlyList<string> MissingPermissions);
