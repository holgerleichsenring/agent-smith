namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-08-26-7a51: a role the DIRECTORY gave somebody, and which claim it came through.
/// The origin is what the surface colours by: a role granted here can be taken back here,
/// and one the directory sends cannot.
/// </summary>
/// <param name="Via">The claim the role arrived through — the role claim's name, or the group claim's.</param>
public sealed record AccessRoleOriginView(string Role, string Via);
