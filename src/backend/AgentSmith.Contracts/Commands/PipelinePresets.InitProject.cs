namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // p0130c: InitProject migrates from BootstrapProjectHandler to SkillRound dispatch.
    // AnalyzeCode populates ProjectMap; PublishProjectLanguage maps PrimaryLanguage to
    // the typed project_language enum; LoadSkills loads the bootstrap-* producers from
    // skills/coding/; BootstrapDispatch deterministically emits exactly one SkillRound
    // for the matching skill (csharp/node/python/generic-bootstrap). The skill writes
    // .agentsmith/context.yaml + coding-principles.md via WriteFile (path-write-guard
    // restricts writes to those two paths); InitCommit then commits the new files.
    //
    // p0161d: BootstrapDiscover slots immediately before BootstrapDispatch. Cold-init
    // runs the read-only project-discovery skill once per repo to enumerate components
    // with evidence; Dispatch then fans out one BootstrapRound per (repo, component).
    // On re-init the handler short-circuits when SandboxDiscoveries already surfaces
    // non-synthetic contexts, so the preset shape stays static — conditional skip
    // lives inside BootstrapDiscoverHandler.
    public static readonly IReadOnlyList<string> InitProject =
    [
        CommandNames.LoadCatalog,
        CommandNames.PipelineNameInitializer,
        CommandNames.CheckoutSource,
        CommandNames.AnalyzeCode,                // populates ProjectMap
        CommandNames.PublishProjectLanguage,     // p0130c: ProjectMap.PrimaryLanguage → project_language enum
        CommandNames.LoadSkills,                 // populates AvailableRoles
        CommandNames.BootstrapDiscover,          // p0161d: read-only component enumeration (per repo)
        CommandNames.BootstrapDispatch,          // p0130c/p0161d: fans out BootstrapRound per (repo, component)
        CommandNames.WriteRunResult,
        CommandNames.InitCommit,
        CommandNames.PrCrossLink, // p0158c: multi-repo init pass-2 (no-op for single-PR runs)
    ];
}
