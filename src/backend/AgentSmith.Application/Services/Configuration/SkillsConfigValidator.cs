using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// 2026-10-12-967ah: refuses a pinned skills release older than the first one that speaks the
/// spec names. From v5.13.0 the skills ask the derivation for "specs" and fill {MaxSpecs}; the
/// readers of the older words are gone, so an older pin would derive nothing. Only a pin that
/// is pulled (the default source) is judged, and only a tag that reads as a version.
/// </summary>
public sealed class SkillsConfigValidator
{
    /// <summary>The first skills release whose prompts use the spec names alone.</summary>
    public const string MinimumVersion = "v5.13.0";

    public IEnumerable<StartupFinding> Findings(SkillsConfig skills)
    {
        ArgumentNullException.ThrowIfNull(skills);
        if (skills.Source != SkillsSourceMode.Default || !TryParse(skills.Version, out var pinned)) yield break;
        if (pinned >= Version.Parse(MinimumVersion.TrimStart('v'))) yield break;
        yield return new StartupFinding(
            StartupSubsystems.Configuration, StartupFindingSeverity.Blocking,
            $"skills.version {skills.Version} predates {MinimumVersion}, the first skills release whose prompts "
            + $"use the spec names alone, and this release reads no other. Pin {MinimumVersion} or later.",
            Field: "skills.version");
    }

    private static bool TryParse(string? tag, out Version version)
    {
        version = new Version();
        return !string.IsNullOrWhiteSpace(tag) && Version.TryParse(tag.Trim().TrimStart('v', 'V'), out version!);
    }
}
