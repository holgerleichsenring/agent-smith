using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-11-17-9a39a: a project's written mode keys read into their enums — a value the project cannot be read by is a
/// blocking finding naming it, as a bad reference is. 2026-11-25-9992i: requires_start and review (moved here from
/// <see cref="ResolvedProjectBuilder"/>).
/// </summary>
internal static class ProjectModeResolver
{
    public static RequiresStart? RequiresStart(string project, string? written, List<StartupFinding> findings)
    {
        if (RequiresStartNames.Parse(written) is { } mode) return mode;
        findings.Add(ProjectFindings.Blocking(project, "requires_start",
            $"Project '{project}': requires_start '{written}' is neither '{RequiresStartNames.OnGreenPush}' nor '{RequiresStartNames.OnMerge}'."));
        return null;
    }

    public static ReviewMode? Review(string project, string? written, List<StartupFinding> findings)
    {
        if (ReviewModeNames.Parse(written) is { } mode) return mode;
        findings.Add(ProjectFindings.Blocking(project, "review",
            $"Project '{project}': review '{written}' is none of {string.Join(", ", ReviewModeNames.All)}."));
        return null;
    }
}
