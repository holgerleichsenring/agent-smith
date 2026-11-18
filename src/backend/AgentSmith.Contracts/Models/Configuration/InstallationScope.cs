namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-17-f3a8a: the reserved label of the installation itself — the admin assistant's conversations and the
/// runs it starts are about agent-smith, not about one project, and carry this label where a project name goes. A
/// configured project may not take it, so the label never names two things.
/// </summary>
public static class InstallationScope
{
    public const string Name = "@agent-smith";

    /// <summary>Whether a project name is the installation's label.</summary>
    public static bool Is(string? name) => string.Equals(name, Name, StringComparison.Ordinal);
}
