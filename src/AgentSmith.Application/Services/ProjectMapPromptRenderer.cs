using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Turns a ProjectMap into prompt-friendly text blocks for skill rounds.
/// Tester and other gates consume the ExistingTests block as evidence so
/// coverage discussion is fact-based — not hallucinated from priors.
/// </summary>
public static class ProjectMapPromptRenderer
{
    public static string RenderExistingTests(ProjectMap? map)
    {
        if (map is null) return string.Empty;
        if (map.TestProjects.Count == 0)
            return "No test projects discovered in this repository.";

        var lines = new List<string> { "Test projects discovered in the repository:" };
        foreach (var t in map.TestProjects)
        {
            var sample = string.IsNullOrEmpty(t.SampleTestPath) ? "" : $", sample: {t.SampleTestPath}";
            lines.Add($"  - {t.Path} ({t.Framework}, {t.FileCount} test file(s){sample})");
        }
        return string.Join('\n', lines);
    }
}
