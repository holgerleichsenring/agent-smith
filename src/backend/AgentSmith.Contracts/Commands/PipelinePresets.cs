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
            ["pr-review"] = PrReview,
            [SpecDialogName] = SpecDialog,
            [PhaseExecutionName] = PhaseExecution,
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
        // p0167a: findings-emitting like the scan presets — review output is
        // structured observations rendered as PR comments, not code changes.
        ["pr-review"] = PipelineType.Structured,
        [SpecDialogName] = PipelineType.Discussion,
        [PhaseExecutionName] = PipelineType.Hierarchical,
    };

    /// <summary>
    /// Returns the pipeline interaction type. Defaults to Discussion for unknown pipelines.
    /// </summary>
    public static PipelineType GetPipelineType(string pipelineName) =>
        PipelineTypes.GetValueOrDefault(pipelineName, PipelineType.Discussion);

    // p0241: the keystone keys "is this a code-changing run?" / "must its tests be
    // green?" off an explicit allow-list, NOT off PipelineType (an interaction-
    // pattern enum) — coupling the success rule to the interaction shape would be
    // fragile. fix-no-test changes code but deliberately skips the test gate.
    private static readonly HashSet<string> CodeChangingPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        "fix-bug", "fix-no-test", "add-feature", PhaseExecutionName,
    };

    private static readonly HashSet<string> GreenTestPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        "fix-bug", "add-feature", PhaseExecutionName,
    };

    /// <summary>
    /// p0241: true when the preset is expected to modify source (a run that ships
    /// nothing is a failure, not a hollow success). False for read-only presets
    /// (security/legal/mad/init) which legitimately finish with zero changes.
    /// </summary>
    public static bool ExpectsCodeChanges(string pipelineName) =>
        CodeChangingPresets.Contains(pipelineName);

    /// <summary>
    /// p0241: true when a successful run additionally requires a green build/test
    /// verdict. Excludes fix-no-test (changes code but skips tests by design).
    /// </summary>
    public static bool ExpectsGreenTests(string pipelineName) =>
        GreenTestPresets.Contains(pipelineName);

    /// <summary>
    /// p0312a: every pipeline resolves its skills from the catalog root, because
    /// every skill lives under <c>skills/_masters/</c> as of catalog 4.0.0. The
    /// per-category map this replaces was already fiction for the p0179-collapsed
    /// presets, and it mis-mapped skill-manager/autonomous to skills/coding so their
    /// own role skills never loaded through the default path at all.
    /// </summary>
    public const string DefaultSkillsPath = "skills";

    /// <summary>
    /// Returns the default skills path for a given pipeline name. One root for all
    /// of them — the parameter stays so callers and project overrides keep their
    /// shape while the resolution is uniform.
    /// </summary>
    public static string GetDefaultSkillsPath(string pipelineName) => DefaultSkillsPath;

    /// <summary>
    /// p0312a: presets that were removed rather than renamed, with the reason a
    /// configuration naming one still validates instead of failing at load.
    /// skill-manager and autonomous carried the Triage/SkillRound choreography that
    /// no longer exists; reactivating either means authoring a master and declaring
    /// an <c>AgenticMaster</c> preset, not restoring this machinery.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> RetiredPresets =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["skill-manager"] =
                "skill-manager was retired in p0312a together with the Triage/SkillRound "
                + "machinery it was the last consumer of. Re-enable it by authoring a "
                + "skill-manager master and declaring an AgenticMaster-shaped preset.",
            ["autonomous"] =
                "autonomous was retired in p0312a together with the Triage/SkillRound "
                + "machinery it was the last consumer of. Re-enable it by authoring an "
                + "autonomous master and declaring an AgenticMaster-shaped preset.",
        };

    /// <summary>The operator-facing reason a retired preset name no longer resolves.</summary>
    public static string? RetiredReason(string pipelineName) =>
        RetiredPresets.GetValueOrDefault(pipelineName);

}
