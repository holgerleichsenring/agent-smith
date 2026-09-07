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
        RepositorySearchTool search, RepositoryFileReadTool read, DependencyAuditTool audit) =>
        [
            BoundedResultTool.Wrap(AIFunctionFactory.Create(
                search.SearchRepository, name: RepositorySearchTool.Name)),
            BoundedResultTool.Wrap(AIFunctionFactory.Create(
                read.ReadFile, name: RepositoryFileReadTool.Name)),
            BoundedResultTool.Wrap(AIFunctionFactory.Create(
                audit.AuditDependencies, name: DependencyAuditTool.Name)),
        ];
}
