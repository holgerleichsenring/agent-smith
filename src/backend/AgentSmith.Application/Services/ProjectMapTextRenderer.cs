using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Renders a <see cref="ProjectMap"/> into the compact code-map text the
/// masters consume ({CodeMapSection}). Pure mapping — shared by the analyzer
/// step (fresh map) and the spec-dialog tier-1 grounding step (cached map).
/// </summary>
public static class ProjectMapTextRenderer
{
    public static string ToCodeMapText(ProjectMap map) =>
        $"primary_language: {map.PrimaryLanguage}\n" +
        $"frameworks: [{string.Join(", ", map.Frameworks)}]\n" +
        $"modules:\n" +
        string.Join('\n', map.Modules.Select(m => $"  - path: {m.Path}\n    role: {m.Role}")) +
        (map.TestProjects.Count == 0 ? "" :
            "\ntest_projects:\n" +
            string.Join('\n', map.TestProjects.Select(t =>
                $"  - path: {t.Path}\n    framework: {t.Framework}\n    file_count: {t.FileCount}")));
}
