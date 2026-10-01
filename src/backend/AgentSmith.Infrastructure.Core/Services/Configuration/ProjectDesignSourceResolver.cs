using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-10-01-7f7aa: a project's <c>design_sources:</c> names resolved against the catalog.
/// An unknown name is a blocking finding on the project, like any other catalog reference,
/// and the project drops out rather than starting without the access it declared.
/// </summary>
internal static class ProjectDesignSourceResolver
{
    public static IReadOnlyList<DesignSource>? Resolve(
        string project, IReadOnlyList<string> names,
        IReadOnlyDictionary<string, DesignSource> catalog, List<StartupFinding> findings)
    {
        var result = new List<DesignSource>(names.Count);
        var anyError = false;
        foreach (var name in names.Distinct(ConfigNames.Comparer))
        {
            if (catalog.TryGetValue(name, out var source))
            {
                result.Add(source);
                continue;
            }
            findings.Add(ProjectFindings.Blocking(project, "design_sources",
                $"Project '{project}': references design source '{name}' which is not defined in design_sources: catalog."));
            anyError = true;
        }
        return anyError ? null : result;
    }
}
