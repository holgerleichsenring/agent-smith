namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-10-15-4a4da: roles held in ONE project, by directory groups and named people. It sits
/// beside the global grants rather than narrowing them, so every mapping stored before it
/// keeps its meaning. Only the roles' project-scopable permissions apply; the rest of a
/// bundle is global business and is dropped.
/// </summary>
public sealed class ProjectBinding
{
    /// <summary>The configured project key. Compared case-insensitively, as project names are.</summary>
    public string Project { get; set; } = string.Empty;

    /// <summary>Group values whose members hold <see cref="Roles"/> in the project.</summary>
    public List<string> Groups { get; set; } = [];

    /// <summary>People who hold <see cref="Roles"/> in the project.</summary>
    public List<PersonRef> People { get; set; } = [];

    /// <summary>The role names held in the project, built-in or custom.</summary>
    public List<string> Roles { get; set; } = [];
}
