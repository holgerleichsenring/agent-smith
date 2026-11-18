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
/// </summary>
public sealed class CredentialResolver(
    Func<ISecretValues> secrets, IMaterializingSecrets inFlight, Func<string, string?>? environment = null)
    : ICredentialResolver
{
    private readonly Func<string, string?> _environment = environment ?? System.Environment.GetEnvironmentVariable;

    public CredentialResolver(ISecretValues secrets, Func<string, string?>? environment = null)
        : this(() => secrets, new MaterializingSecrets(), environment) { }

    public string For(RepoConnection repo) => Resolve($"Repo '{repo.Name}'", repo.Auth);

    public string For(ResolvedConnection connection) =>
        Resolve($"Connection '{connection.Name}'", connection.Auth);

    public string For(TrackerConnection tracker) => Resolve($"Tracker '{tracker.Name}'", tracker.Auth);

    public string? Named(string name) => Lookup(name).Value;

    public CredentialLookup Lookup(string name) => EnvironmentFirst.Lookup(name, _environment, inFlight.Current ?? secrets());

    private string Resolve(string entity, string secret) =>
        (string.IsNullOrWhiteSpace(secret) ? null : (inFlight.Current ?? secrets()).Resolve(secret))
        ?? throw new MissingCredentialException(entity, secret);
}
