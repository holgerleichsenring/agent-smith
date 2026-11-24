using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>
/// 2026-11-23-933db: the role-mapping side of a PoC project — whether a binding names this person for a project, whether
/// any binding targets it, and the mapping with this person's reader binding appended. The edited mapping is a COPY:
/// the source hands one instance to every request and reads its identity as "nothing changed".
/// </summary>
internal static class PocProjectBinding
{
    public static bool Names(RoleMappingConfig mapping, string project, PersonRef person) =>
        mapping.ProjectBindings.Any(b => string.Equals(b.Project, project, StringComparison.OrdinalIgnoreCase)
            && b.People.Any(p => p.Claim == person.Claim && p.Value == person.Value));

    public static bool Targets(RoleMappingConfig mapping, string project) =>
        mapping.ProjectBindings.Any(b => string.Equals(b.Project, project, StringComparison.OrdinalIgnoreCase));

    public static RoleMappingConfig With(RoleMappingConfig mapping, string project, PersonRef person)
    {
        var copy = JsonSerializer.Deserialize<RoleMappingConfig>(JsonSerializer.Serialize(mapping)) ?? new RoleMappingConfig();
        copy.ProjectBindings.Add(new ProjectBinding
        {
            Project = project, People = [new PersonRef { Claim = person.Claim, Value = person.Value }], Roles = [BuiltInRoles.Reader],
        });
        return copy;
    }
}
