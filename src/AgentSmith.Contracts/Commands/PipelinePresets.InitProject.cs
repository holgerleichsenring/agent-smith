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
    public static readonly IReadOnlyList<string> InitProject =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.CheckoutSource,
        CommandNames.AnalyzeCode,                // populates ProjectMap
        CommandNames.PublishProjectLanguage,     // p0130c: ProjectMap.PrimaryLanguage → project_language enum
        CommandNames.LoadSkills,                 // populates AvailableRoles
        CommandNames.BootstrapDispatch,          // p0130c: emits SkillRound for the matching bootstrap skill
        CommandNames.WriteRunResult,
        CommandNames.InitCommit,
    ];
}
