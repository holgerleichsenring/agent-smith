using System.Collections.Concurrent;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Returns a skill's body verbatim. p0127c collapsed multi-role skills into
/// single-body files; p0131a retired the legacy <c>{{ref:&lt;Id&gt;}}</c>
/// placeholder mechanism along with the <c>References</c> frontmatter field.
/// The cache stays — body string interning across many SkillRound dispatches
/// is still cheap and removes per-call allocations.
/// </summary>
public sealed class SkillBodyResolver : ISkillBodyResolver
{
    private readonly ConcurrentDictionary<(string Skill, SkillRole Role), string> _cache = new();

    public string ResolveBody(RoleSkillDefinition skill, SkillRole role) =>
        _cache.GetOrAdd((skill.Name, role), _ => skill.Rules);
}
