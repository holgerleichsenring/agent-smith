using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// What a hand-back comes to when the run has no tracker to park the ticket on. Lifted out of
/// <see cref="SpecHandbackHandler"/>, which was at its length limit.
/// <para>2026-10-12-d142: a scope nobody divided fails like a refusal — continuing would run checkout
/// with no spec to work. 2026-10-12-f5f7f: so does a series out of step.</para>
/// </summary>
internal static class SpecHandbackWithoutTracker
{
    public static CommandResult Result(SpecHandback handback) => handback.Case switch
    {
        SpecHandbackCase.Refused => CommandResult.Fail(
            $"Refused: {handback.Reason} — quoted: \"{handback.Quote}\" — "
            + "and the run has no tracker to park the ticket on"),
        SpecHandbackCase.ScopeUnassigned or SpecHandbackCase.SeriesOutOfStep => CommandResult.Fail(
            $"{handback.Reason} — and the run has no tracker to park the ticket on"),
        _ => CommandResult.Ok($"Derivation handed back ({handback.Case}) but the run has no tracker"),
    };
}
