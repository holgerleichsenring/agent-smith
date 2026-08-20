using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0482: what the delivery account is handed to answer with, and how many turns it gets.
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

    /// <summary>Null rather than an empty list when there is no sandbox: an empty Tools
    /// collection and no Tools collection are the same call, and null is what says the
    /// account fell back to the cited evidence.</summary>
    internal static IList<AITool>? For(BranchSearch? search) =>
        search is null
            ? null
            : [AIFunctionFactory.Create(search.SearchBranch, name: "search_branch")];
}
