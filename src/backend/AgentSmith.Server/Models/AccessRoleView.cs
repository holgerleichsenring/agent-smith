namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-08-26-7a51: one role name, what it holds, and how many people and groups carry it.
/// A custom role reports <c>BuiltIn: false</c> and is rendered read-only — it survives a
/// save verbatim, and a new one is refused.
/// </summary>
public sealed record AccessRoleView(
    string Name, bool BuiltIn, IReadOnlyList<string> Permissions, int People, int Groups);
