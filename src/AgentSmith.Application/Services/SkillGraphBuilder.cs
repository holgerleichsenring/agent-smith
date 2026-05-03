using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services;

/// <summary>
/// Builds a deterministic execution graph from skill orchestration metadata.
/// Resolves runs_after/runs_before dependencies (role types and skill names),
/// performs topological sort, and groups parallel skills into stages.
/// </summary>
public sealed class SkillGraphBuilder : ISkillGraphBuilder
{
    public SkillGraph Build(IReadOnlyList<RoleSkillDefinition> skills)
    {
        if (skills.Count == 0)
            return new SkillGraph(Array.Empty<ExecutionStage>());

        // Resolve orchestration for each skill (default to Contributor if missing)
        var entries = skills.Select(s => (
            Name: s.Name,
            Orch: s.Orchestration ?? SkillOrchestration.DefaultContributor
        )).ToList();

        // Build role-type → skill names lookup
        var skillsByRole = entries
            .GroupBy(e => e.Orch.Role)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var allNames = entries.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orchByName = entries.ToDictionary(e => e.Name, e => e.Orch, StringComparer.OrdinalIgnoreCase);

        // Build adjacency: dependencies[skill] = set of skills it must wait for
        var dependencies = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var deps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dep in entry.Orch.RunsAfter)
            {
                if (Enum.TryParse<OrchestrationRole>(dep, ignoreCase: true, out var role))
                {
                    // Role reference → expand to all skills with that role
                    if (skillsByRole.TryGetValue(role, out var roleSkills))
                        foreach (var s in roleSkills)
                            if (s != entry.Name) deps.Add(s);
                }
                else if (allNames.Contains(dep))
                {
                    deps.Add(dep);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Skill '{entry.Name}' has unresolved runs_after reference: '{dep}'");
                }
            }

            dependencies[entry.Name] = deps;
        }

        // Also process runs_before (reverse edges)
        foreach (var entry in entries)
        {
            foreach (var dep in entry.Orch.RunsBefore)
            {
                IEnumerable<string> targets;

                if (Enum.TryParse<OrchestrationRole>(dep, ignoreCase: true, out var role))
                {
                    targets = skillsByRole.TryGetValue(role, out var roleSkills)
                        ? roleSkills.Where(s => s != entry.Name)
                        : Enumerable.Empty<string>();
                }
                else if (allNames.Contains(dep))
                {
                    targets = [dep];
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Skill '{entry.Name}' has unresolved runs_before reference: '{dep}'");
                }

                foreach (var target in targets)
                {
                    if (!dependencies.ContainsKey(target))
                        dependencies[target] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    dependencies[target].Add(entry.Name);
                }
            }
        }

        // Topological sort (Kahn's algorithm)
        var stages = TopologicalSort(allNames, dependencies, orchByName);

        return new SkillGraph(stages);
    }

    private static IReadOnlyList<ExecutionStage> TopologicalSort(
        HashSet<string> allNames,
        Dictionary<string, HashSet<string>> dependencies,
        Dictionary<string, SkillOrchestration> orchByName)
    {
        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var dependents = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in allNames)
        {
            inDegree[name] = dependencies.TryGetValue(name, out var deps) ? deps.Count : 0;
            dependents[name] = [];
        }

        foreach (var (skill, deps) in dependencies)
        {
            foreach (var dep in deps)
            {
                if (!dependents.ContainsKey(dep))
                    dependents[dep] = [];
                dependents[dep].Add(skill);
            }
        }

        var stages = new List<ExecutionStage>();
        var remaining = new HashSet<string>(allNames, StringComparer.OrdinalIgnoreCase);
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (remaining.Count > 0)
        {
            // Collect all skills with zero in-degree
            var ready = remaining.Where(n => inDegree[n] == 0).ToList();

            if (ready.Count == 0)
            {
                var cycle = string.Join(", ", remaining);
                throw new InvalidOperationException(
                    $"Cycle detected in skill graph. Remaining skills: {cycle}");
            }

            // Group ready skills by role for stage creation
            var grouped = GroupIntoStages(ready, orchByName);
            stages.AddRange(grouped);

            foreach (var name in ready)
            {
                remaining.Remove(name);
                processed.Add(name);

                foreach (var dependent in dependents[name])
                {
                    inDegree[dependent]--;
                }
            }
        }

        return stages;
    }

    private static IEnumerable<ExecutionStage> GroupIntoStages(
        List<string> readySkills,
        Dictionary<string, SkillOrchestration> orchByName)
    {
        // Group by role — different roles at the same dependency level
        // still get separate stages (lead before contributor, gate after contributor)
        var byRole = readySkills
            .GroupBy(s => orchByName[s].Role)
            .OrderBy(g => RolePriority(g.Key));

        foreach (var group in byRole)
        {
            var isGate = group.Key == OrchestrationRole.Gate;
            var isLead = group.Key == OrchestrationRole.Lead;
            var isExecutor = group.Key == OrchestrationRole.Executor;
            yield return new ExecutionStage(group.ToList(), isGate, isLead, isExecutor);
        }
    }

    /// <summary>
    /// Execution priority: Lead first, then Contributors, then Gates, then Executors.
    /// </summary>
    private static int RolePriority(OrchestrationRole role) => role switch
    {
        OrchestrationRole.Lead => 0,
        OrchestrationRole.Contributor => 1,
        OrchestrationRole.Gate => 2,
        OrchestrationRole.Executor => 3,
        _ => 4,
    };
}
