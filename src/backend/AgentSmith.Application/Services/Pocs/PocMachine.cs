using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-25-0873: the identity a PoC act's source repository acts under. A repository its poc- project already stamped keeps
/// that project's machine; any other takes <c>pocs.machine</c>'s entry for its hoster; with neither it keeps its own token,
/// which the setup says (<c>PocMachineFindings</c>).
/// </summary>
public static class PocMachine
{
    public static RepoConnection Stamp(PocsConfig pocs, RepoConnection repo)
    {
        ArgumentNullException.ThrowIfNull(pocs);
        ArgumentNullException.ThrowIfNull(repo);
        return repo.Machine is not null || repo.Type == RepoType.Local ? repo : repo with { Machine = pocs.Machine?.IdentityFor(repo.Type) };
    }
}
