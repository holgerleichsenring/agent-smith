using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-02-5f89a: <see cref="ICredentialResolver"/> over <see cref="ISecretValues"/> — the
/// server's follows the config store, the CLI's is the file it loaded — so this class decides
/// only which secret an entity names and how its absence is said. While a configuration is being
/// materialized, its own secrets answer first (<see cref="IMaterializingSecrets"/>). The values
/// are reached through a function because the configuration they come from is itself built
/// through discovery, which asks here for its token.
/// <para>2026-11-25-9992h: a repository stamped with its project's machine identity answers the machine's token — a GitHub
/// App's installation token minted through <see cref="IMachineTokens"/> — so every act goes under the machine.</para>
/// </summary>
public sealed class CredentialResolver(
    Func<ISecretValues> secrets, IMaterializingSecrets inFlight, Func<string, string?>? environment = null,
    IMachineTokens? machineTokens = null)
    : ICredentialResolver
{
    private readonly Func<string, string?> _environment = environment ?? System.Environment.GetEnvironmentVariable;

    public CredentialResolver(ISecretValues secrets, Func<string, string?>? environment = null, IMachineTokens? machineTokens = null)
        : this(() => secrets, new MaterializingSecrets(), environment, machineTokens) { }

    public string For(RepoConnection repo) => repo.Machine is { } machine
        ? Machine(repo, machine)
        : Resolve($"Repo '{repo.Name}'", repo.Auth);

    public string? MachineLogin(RepoConnection repo) =>
        repo.Machine is { IsGitHubApp: true } machine
            ? Tokens(repo, machine).AppLogin(repo, machine, Resolve(Entity(repo, machine), machine.Auth))
            : null;

    private string Machine(RepoConnection repo, MachineIdentity machine)
    {
        var secret = Resolve(Entity(repo, machine), machine.Auth);
        return machine.IsGitHubApp ? Tokens(repo, machine).InstallationToken(repo, machine, secret) : secret;
    }

    private IMachineTokens Tokens(RepoConnection repo, MachineIdentity machine) => machineTokens
        ?? throw new MachineCredentialException(Entity(repo, machine), machine.Auth, "this process mints no GitHub App tokens");

    private static string Entity(RepoConnection repo, MachineIdentity machine) => $"Repo '{repo.Name}' (machine identity '{machine.Name}')";

    public string For(ResolvedConnection connection) =>
        Resolve($"Connection '{connection.Name}'", connection.Auth);

    public string For(TrackerConnection tracker) => Resolve($"Tracker '{tracker.Name}'", tracker.Auth);

    public string? Named(string name) => Lookup(name).Value;

    public CredentialLookup Lookup(string name) => EnvironmentFirst.Lookup(name, _environment, inFlight.Current ?? secrets());

    private string Resolve(string entity, string secret) =>
        (string.IsNullOrWhiteSpace(secret) ? null : (inFlight.Current ?? secrets()).Resolve(secret))
        ?? throw new MissingCredentialException(entity, secret);
}
