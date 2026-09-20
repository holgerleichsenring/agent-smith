using AgentSmith.Application.Services.Tools;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: what the derivation call is handed to look with — exactly the three
/// named read-only tools, each bounded in what one result may return, and no shell.
/// </summary>
internal static class DerivationTools
{
    /// <summary>Null rather than an empty list when there is nothing to look at: an
    /// empty Tools collection and no Tools collection are the same call, and null is
    /// what says the derivation wrote from the ticket and the code map alone.</summary>
    internal static IList<AITool>? For(DerivationLook? look) => look?.Tools;

    internal static IList<AITool> Over(
        RepositorySearchTool search, RepositoryFileReadTool read, DependencyAuditTool? audit,
        VerifyStageRunTool? stage = null) =>
        [
            BoundedResultTool.Wrap(AIFunctionFactory.Create(
                search.SearchRepository, RepositorySearchTool.Name, search.Description)),
            BoundedResultTool.Wrap(AIFunctionFactory.Create(
                read.ReadFile, RepositoryFileReadTool.Name, read.Description)),
            .. audit is null
                ? []
                : new[] { BoundedResultTool.Wrap(AIFunctionFactory.Create(
                    audit.AuditDependencies, DependencyAuditTool.Name, audit.Description)) },
            // 2026-09-20-9c74: and, for the one holder handed the collaborator, a run of one
            // verify stage the repository DECLARED. The tool tails its own output, so the
            // wrapper's larger FRONT-keeping bound never reaches it.
            .. stage is null
                ? []
                : new[] { BoundedResultTool.Wrap(AIFunctionFactory.Create(
                    stage.RunVerifyStage, VerifyStageRunTool.Name, stage.Description)) },
        ];

    /// <summary>2026-09-17-042ed: what the tools a look carries are, in the prompt's words.
    /// 2026-09-20-9c74: the stage run is named off the TERMS — the collaborator is what
    /// withholds the capability, and the term is what the holder is told.</summary>
    internal static string Named(DerivationLook look) =>
        (look.Tools.Any(tool => tool.Name == DependencyAuditTool.Name)
            ? "a search, a file read, the ecosystem's own dependency audit"
            : "a search and a file read")
        + (look.Terms.MayRunAStage
            ? ", and a run of ONE verify stage a repository declared"
            : string.Empty);
}
