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
    // Field initialization order across partial files is unspecified by the C# spec, so All and
    // Routable are populated in a static constructor — guaranteed to run AFTER every per-preset
    // field initializer whatever file order the compiler chose.
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
        Routable = [.. Names.Where(n => !NeedsHostSuppliedContext.Contains(n))];
    }

    public static IReadOnlyList<string>? TryResolve(string name) => All.GetValueOrDefault(name);

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
        PipelineTypes.GetValueOrDefault(pipelineName, PipelineType.Discussion);

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
        CodeChangingPresets.Contains(pipelineName);

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

    /// <summary>2026-09-16-a4d7: what a ticket runs when neither the tracker nor the project's
    /// trigger declares a fallback. PipelineResolver is the only place that ANSWERS with it; the
    /// name lives here so the studio's draft rules can report it across the assembly boundary.
    /// 2026-09-25-e5b1: it is the code preset itself now — it was the alias `fix-bug`, which is
    /// the same pipeline said in a word that no longer resolves.</summary>
    public const string UndeclaredFallbackPipeline = CodeName;

    /// <summary>
    /// Every name a CONFIGURATION may legitimately carry. 2026-09-25-e5b1: that is now exactly
    /// the presets — a retired name is no longer accepted anywhere, and a configuration still
    /// carrying one is reported by <c>RoutingPipelineNames</c> with the name that replaced it.
    /// Distinct from <see cref="Names"/> only in what it is ASKED: this is what may be stored,
    /// <see cref="Routable"/> is what a ticket may be routed to, and <see cref="Names"/> is what
    /// the studio offers.
    /// </summary>
    public static bool IsAcceptedName(string pipelineName) => All.ContainsKey(pipelineName);

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
