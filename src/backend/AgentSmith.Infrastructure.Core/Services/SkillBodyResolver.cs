using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Returns a skill's body with its <c>{{ref:&lt;slug&gt;}}</c> citations inlined
/// from the catalog's <c>references/</c> directory.
///
/// p0131a retired an earlier placeholder mechanism that resolved ids declared in a
/// <c>References</c> frontmatter field; p0313b brings citation back on a different
/// basis — the reference is a FILE in the catalog, the body is the only declaration,
/// and resolution goes exactly ONE level deep. A reference that could cite another
/// would make prompt assembly a graph nobody can read at the point of use, and the
/// failure mode (a cycle) would only show up at render time.
///
/// Both failure modes are loud. A cited reference the catalog does not ship, and a
/// reference that cites another, throw instead of rendering — a master silently
/// missing the rules it cites is a master that goes to the model without them.
///
/// The cache stays: body string interning across many dispatches is cheap, and a
/// reference is static for the life of a pinned catalog.
/// </summary>
public sealed class SkillBodyResolver(ISkillReferenceSource references) : ISkillBodyResolver
{
    private static readonly Regex Citation =
        new(@"\{\{ref:([^}]*)\}\}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    // A slug names a file. Constraining it to lower-case words keeps a citation
    // from reaching outside references/ — the source composes a path from it.
    private static readonly Regex Slug =
        new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private readonly ConcurrentDictionary<(string Skill, SkillRole Role), string> _cache = new();

    public string ResolveBody(RoleSkillDefinition skill, SkillRole role) =>
        _cache.GetOrAdd((skill.Name, role), _ => Inline(skill.Name, skill.Rules));

    private string Inline(string skillName, string body) =>
        string.IsNullOrEmpty(body)
            ? body
            : Citation.Replace(body, match => Resolve(skillName, match.Groups[1].Value));

    private string Resolve(string skillName, string slug)
    {
        if (!Slug.IsMatch(slug))
            throw new InvalidOperationException(
                $"Skill '{skillName}' cites reference '{slug}', which is not a valid reference name. " +
                "A reference name is lower-case words joined by single hyphens and resolves to " +
                $"references/<name>.md in the skill catalog.");

        var body = references.TryRead(slug)
            ?? throw new InvalidOperationException(
                $"Skill '{skillName}' cites reference '{slug}', but the loaded skill catalog has no " +
                $"references/{slug}.md. Pin a skills.version that ships it — rendering the master " +
                "without the methodology it cites would send the model a prompt missing its rules.");

        if (Citation.IsMatch(body))
            throw new InvalidOperationException(
                $"Reference '{slug}' (cited by skill '{skillName}') cites another reference. " +
                "References are inlined one level deep: move the shared text into this file, or " +
                "cite both references from the master.");

        return body.TrimEnd();
    }
}
