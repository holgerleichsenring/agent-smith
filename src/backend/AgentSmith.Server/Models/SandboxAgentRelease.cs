namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-08-27-729e: which sandbox-agent build ONE project spawns. There is no project-free
/// agent version — the resolver takes a project, because a pin is declared per project —
/// so the report states one line per configured project rather than a single number it
/// would have had to invent.
/// </summary>
/// <param name="Project">The configured project this line answers for.</param>
/// <param name="Version">The image tag, or null when this build cannot say.</param>
/// <param name="Source">One of <see cref="Pinned"/>, <see cref="Derived"/>, <see cref="Underivable"/>.</param>
public sealed record SandboxAgentRelease(string Project, string? Version, string Source)
{
    /// <summary>An operator declared the tag.</summary>
    public const string Pinned = "pinned";

    /// <summary>The tag follows the release this server is.</summary>
    public const string Derived = "derived";

    /// <summary>Neither — a build that was not stamped with a release has nothing to
    /// derive from, and this reports that rather than guessing a tag.</summary>
    public const string Underivable = "underivable";
}
