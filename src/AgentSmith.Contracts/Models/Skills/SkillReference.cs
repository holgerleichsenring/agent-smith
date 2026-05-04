namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Reference file declared in a skill's frontmatter. Body cites it via {{ref:&lt;Id&gt;}}
/// placeholders that SkillBodyResolver inlines at prompt-build time.
/// Path is relative to the skill's directory.
/// </summary>
public sealed record SkillReference(string Id, string Path);
