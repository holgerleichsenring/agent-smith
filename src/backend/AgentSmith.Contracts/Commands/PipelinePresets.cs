using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Code-defined pipeline presets. YAML pipelines section is an optional override.
/// Each preset's command list lives in its own partial file
/// (PipelinePresets.FixBug.cs etc.) so adding/removing a step is a one-file change
/// that doesn't touch any other preset. This file holds the resolver, the default-
/// skills-path map, the pipeline-type map, and the single-phase classifier.
/// </summary>
public static partial class PipelinePresets
{
    // Field initialization order across partial files is unspecified by the C# spec,
    // so the All dictionary is populated in a static constructor — guaranteed to run
    // AFTER every per-preset field's initializer regardless of the compiler's chosen
    // file order.
    private static readonly Dictionary<string, IReadOnlyList<string>> All;

    public static IReadOnlyList<string> Names { get; }

    static PipelinePresets()
    {
        All = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["fix-bug"] = FixBug,
            ["fix-no-test"] = FixNoTest,
            ["init-project"] = InitProject,
            ["add-feature"] = AddFeature,
            ["mad-discussion"] = MadDiscussion,
            ["legal-analysis"] = LegalAnalysis,
            ["security-scan"] = SecurityScan,
            ["api-security-scan"] = ApiSecurityScan,
            ["skill-manager"] = SkillManager,
            ["autonomous"] = Autonomous,
        };
        Names = All.Keys.ToList();
    }

    public static IReadOnlyList<string>? TryResolve(string name) =>
        All.GetValueOrDefault(name);

    private static readonly Dictionary<string, PipelineType> PipelineTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fix-bug"] = PipelineType.Hierarchical,
        ["fix-no-test"] = PipelineType.Hierarchical,
        ["add-feature"] = PipelineType.Hierarchical,
        ["init-project"] = PipelineType.Discussion,
        ["security-scan"] = PipelineType.Structured,
        ["api-security-scan"] = PipelineType.Structured,
        ["mad-discussion"] = PipelineType.Discussion,
        ["legal-analysis"] = PipelineType.Discussion,
        ["skill-manager"] = PipelineType.Discussion,
        ["autonomous"] = PipelineType.Discussion,
    };

    /// <summary>
    /// Returns the pipeline interaction type. Defaults to Discussion for unknown pipelines.
    /// </summary>
    public static PipelineType GetPipelineType(string pipelineName) =>
        PipelineTypes.GetValueOrDefault(pipelineName, PipelineType.Discussion);

    /// <summary>
    /// p0131c-pre: true when the named preset emits a single Plan-phase batch
    /// (no <see cref="CommandNames.RunReviewPhase"/> / <see cref="CommandNames.RunFinalPhase"/>
    /// steps). Drives <c>StructuredTriageStrategy</c>'s phase-collapse logic so
    /// LLM-emitted Review-/Final-phase skill assignments don't get silently
    /// dropped on presets that don't run those phases.
    /// Unknown pipeline names default to <c>true</c> (single-phase) — safer
    /// because emitting Review/Final commands for an unknown preset would
    /// dispatch to handlers that may not be in the run.
    /// </summary>
    public static bool IsSinglePhase(string pipelineName)
    {
        var preset = TryResolve(pipelineName);
        if (preset is null) return true;
        return !preset.Contains(CommandNames.RunReviewPhase, StringComparer.Ordinal)
            && !preset.Contains(CommandNames.RunFinalPhase, StringComparer.Ordinal);
    }

    /// <summary>
    /// Default skills directory per pipeline preset.
    /// Used when no explicit skills_path is configured in the project.
    /// </summary>
    private static readonly Dictionary<string, string> DefaultSkillsPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fix-bug"] = "skills/coding",
        ["fix-no-test"] = "skills/coding",
        ["add-feature"] = "skills/coding",
        ["init-project"] = "skills/coding",
        ["security-scan"] = "skills/security",
        ["api-security-scan"] = "skills/api-security",
        ["legal-analysis"] = "skills/legal",
        ["mad-discussion"] = "skills/mad",
        ["skill-manager"] = "skills/coding",
        ["autonomous"] = "skills/coding",
    };

    /// <summary>
    /// Returns the default skills path for a given pipeline name.
    /// Falls back to "skills/coding" if the pipeline is not mapped.
    /// </summary>
    public static string GetDefaultSkillsPath(string pipelineName) =>
        DefaultSkillsPaths.GetValueOrDefault(pipelineName, "skills/coding");

    /// <summary>
    /// Maps a pipeline name to the SkillRound-family command its handlers expect.
    /// security-scan → SecuritySkillRoundCommand, api-security-scan → ApiSecuritySkillRoundCommand,
    /// everything else → SkillRoundCommand. Filter assignments always emit FilterRoundCommand.
    /// </summary>
    public static string GetSkillRoundCommandName(string pipelineName) => pipelineName.ToLowerInvariant() switch
    {
        "security-scan" => CommandNames.SecuritySkillRound,
        "api-security-scan" => CommandNames.ApiSecuritySkillRound,
        _ => CommandNames.SkillRound
    };
}
