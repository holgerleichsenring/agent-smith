using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Repositories;

/// <summary>
/// 2026-10-12-f5f7c: the run's REPOSITORIES — not its sandbox keys. A repository with two
/// toolchain sandboxes is one name; a template or reference scope is none. Each repository's
/// files are those of the sandbox its commit is made from, so whatever is written there is in
/// the change that repository's pull request carries.
/// </summary>
public interface IRunRepositories
{
    /// <summary>The repository names, read at the moment of the call.</summary>
    IReadOnlyList<string> Names();

    /// <summary>The file surface of the named repository; null when the run has no such one.</summary>
    ISandboxFileReader? FilesOf(string repository);

    /// <summary>True when the named repository has a code change this run, so its commit lands.</summary>
    Task<bool> HasCodeChangeAsync(string repository, CancellationToken cancellationToken);
}
