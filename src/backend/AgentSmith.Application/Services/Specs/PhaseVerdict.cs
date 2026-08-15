using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0420: turns the accounts into the phase's verdict.
/// <para>
/// An outstanding criterion NAMES itself and the repository it is outstanding in — the
/// old gate answered "this run produced no code changes", which is true of every resumed
/// branch and tells the operator nothing about what is actually missing.
/// </para>
/// </summary>
public static class PhaseVerdict
{
    public static CommandResult From(
        CommandResult mechanical, IReadOnlyList<SpecAccount> accounts)
    {
        ArgumentNullException.ThrowIfNull(mechanical);
        ArgumentNullException.ThrowIfNull(accounts);
        if (accounts.Count == 0) return mechanical;

        var unaccounted = accounts.Where(a => a.Problem is not null).ToList();
        if (unaccounted.Count > 0)
            return CommandResult.Fail(
                "The phase could not be accounted for: "
                + string.Join("; ", unaccounted.Select(a => $"{a.RepoKey} — {a.Problem}"))
                + ". An unaccounted phase is not a delivered one.");

        var outstanding = accounts
            .SelectMany(a => a.Outstanding.Select(c => $"{a.RepoKey}: {c.Criterion}"
                + (c.Note is null ? string.Empty : $" ({c.Note})")))
            .ToList();
        if (outstanding.Count > 0)
            return CommandResult.Fail(
                $"{outstanding.Count} criterion(s) of the ratified phase are not satisfied by "
                + $"the branch:\n- {string.Join("\n- ", outstanding)}");

        var satisfied = accounts.Sum(a => a.Criteria.Count);
        return CommandResult.Ok(
            $"{mechanical.Message} — and all {satisfied} ratified criterion(s) are accounted "
            + $"for across {accounts.Count} repo(s).");
    }
}
