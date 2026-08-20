using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0483: what the delivery account is handed to answer with, and how many turns it gets.
/// <para>
/// Separated from <see cref="SpecAccountant"/> because deciding what a reader may DO is not
/// the same question as what its answer is worth, and the accountant was at the file-length
/// ceiling. It is also the one place that has to stay consistent between the first call and
/// the re-ask: an account offered a tool once and not the second time would be asked to
/// correct a citation it could no longer check.
/// </para>
/// </summary>
internal static class AccountTools
{
    /// <summary>Room for every search the account may run, plus the turn that asks and the
    /// turn that answers.</summary>
    internal const int MaxIterations = BranchSearch.MaxSearches + 2;

    /// <summary>What a citation may name: a path the diff covers, a command that was listed,
    /// or a search the account ran itself. p0484: the third was missing, so a criterion the
    /// account had settled by LOOKING could not be reported — it was allowed to look and not
    /// allowed to say so.</summary>
    internal static CitationResolver ResolverOver(
        string diff, IReadOnlyList<string> commandResults, BranchSearch? search) =>
        new(CitedFileIndex.FromDiff(diff),
            search is null ? commandResults : [.. commandResults, .. search.Evidence]);

    /// <summary>Null rather than an empty list when there is no sandbox: an empty Tools
    /// collection and no Tools collection are the same call, and null is what says the
    /// account fell back to the cited evidence.</summary>
    internal static IList<AITool>? For(BranchSearch? search) =>
        search is null
            ? null
            : [AIFunctionFactory.Create(search.SearchBranch, name: "search_branch")];
}
