namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Discriminator between the legacy multi-body SKILL.md shape (roles_supported
/// + ## as_&lt;role&gt; sections) and the new single-body shape (role + verbatim
/// body). Used by <see cref="SkillMdParser"/> for per-file routing.
/// </summary>
internal enum SkillMdFormat
{
    Legacy = 0,
    NewFormat = 1,
}
