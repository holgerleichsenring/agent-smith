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
            [CodeName] = Code,
            ["init-project"] = InitProject,
            ["mad-discussion"] = MadDiscussion,
            ["legal-analysis"] = LegalAnalysis,
            ["security-scan"] = SecurityScan,
            ["api-security-scan"] = ApiSecurityScan,
            ["pr-review"] = PrReview,
            [SpecDialogName] = SpecDialog,
        };
        Names = All.Keys.ToList();
    }

    public static IReadOnlyList<string>? TryResolve(string name) =>
        All.GetValueOrDefault(name)
        ?? (PresetAliases.TryGetValue(name, out var target) ? All.GetValueOrDefault(target) : null);

    private static readonly Dictionary<string, PipelineType> PipelineTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [CodeName] = PipelineType.Hierarchical,
        ["init-project"] = PipelineType.Discussion,
        ["security-scan"] = PipelineType.Structured,
        ["api-security-scan"] = PipelineType.Structured,
        ["mad-discussion"] = PipelineType.Discussion,
        ["legal-analysis"] = PipelineType.Discussion,
        // p0167a: findings-emitting like the scan presets — review output is
        // structured observations rendered as PR comments, not code changes.
        ["pr-review"] = PipelineType.Structured,
        [SpecDialogName] = PipelineType.Discussion,
    };

    /// <summary>
    /// Returns the pipeline interaction type. Defaults to Discussion for unknown pipelines.
    /// </summary>
    public static PipelineType GetPipelineType(string pipelineName) =>
        PipelineTypes.GetValueOrDefault(Canonical(pipelineName), PipelineType.Discussion);

    // p0241: the keystone keys "is this a code-changing run?" / "must its tests be
    // green?" off an explicit allow-list, NOT off PipelineType (an interaction-
    // pattern enum) — coupling the success rule to the interaction shape would be
    // fragile. p0393 collapsed the four coding presets into one, so the list is one
    // entry; it stays a list rather than becoming `== CodeName` because the property
    // being asserted is "this preset ships code", which a future preset may also have.
    private static readonly HashSet<string> CodeChangingPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        CodeName,
    };

    /// <summary>
    /// p0241: true when the preset is expected to modify source. p0421: this no longer
    /// decides whether a run DELIVERED — that is read from the accounts every phase gives
    /// against the branch, for every preset alike. What is left is what the answer is
    /// actually about: whether a run has a master loop to re-drive and needs a sandbox
    /// sized for building.
    /// </summary>
    public static bool ExpectsCodeChanges(string pipelineName) =>
        CodeChangingPresets.Contains(Canonical(pipelineName));

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
    /// p0393: preset names that RESOLVE to another preset. An operator configuration
    /// naming one keeps working and logs the replacement — renaming by alias rather than
    /// by breaking configuration, because preset names live in triggers (default_pipeline)
    /// and projects that agent-smith does not own.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> PresetAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fix-bug"] = CodeName,
            ["fix-no-test"] = CodeName,
            ["add-feature"] = CodeName,
            [PhaseExecutionName] = CodeName,
        };

    /// <summary>The preset an alias resolves to, or null when the name is not an alias.</summary>
    public static string? ResolveAlias(string pipelineName) =>
        PresetAliases.GetValueOrDefault(pipelineName);

    /// <summary>
    /// The canonical preset name: an alias resolved to its target, anything else unchanged.
    /// EVERY per-preset classification goes through this. Resolving the command list while
    /// classifying the raw name would make an alias half-real — the run would execute `code`
    /// but be sized as a non-code pipeline and judged by a keystone that expects no diff,
    /// which is worse than not aliasing at all.
    /// </summary>
    public static string Canonical(string pipelineName) =>
        PresetAliases.GetValueOrDefault(pipelineName, pipelineName);

    /// <summary>
    /// Every name a CONFIGURATION may legitimately carry: the current presets plus the
    /// retired aliases. Distinct from <see cref="Names"/>, which is what agent-smith
    /// OFFERS — an alias must keep validating and must not be presented as a choice.
    /// </summary>
    public static bool IsAcceptedName(string pipelineName) =>
        All.ContainsKey(pipelineName) || PresetAliases.ContainsKey(pipelineName);

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
