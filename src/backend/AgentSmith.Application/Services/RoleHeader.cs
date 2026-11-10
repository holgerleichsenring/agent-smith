using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-11-09-0620g: the head of a bootstrap round's system prompt. It used to read "{DisplayName}: {Description}" — a
/// line starting with ": " for a skill without a display name — and label the whole skill "Role-Specific Rules". The
/// skill's body is its methodology, so it follows under the role's name and what it is for.
/// </summary>
internal static class RoleHeader
{
    internal static string Render(RoleSkillDefinition role)
    {
        var name = string.IsNullOrWhiteSpace(role.DisplayName) ? role.Name : role.DisplayName;
        var who = string.IsNullOrWhiteSpace(role.Description) ? name : $"{name} — {role.Description}";
        return $"## Your role\n{who}\n\n{role.Rules}";
    }
}
