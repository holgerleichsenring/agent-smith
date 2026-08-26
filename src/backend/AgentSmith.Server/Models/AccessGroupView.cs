namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-08-26-7a51: one group value — mapped onto roles, carried by observed callers, or
/// both. A value nobody has carried is typed in; a value carried by somebody is picked.
/// </summary>
/// <param name="Carriers">How many observed callers arrived with this value.</param>
public sealed record AccessGroupView(
    string Value, IReadOnlyList<string> Roles, int Carriers);
