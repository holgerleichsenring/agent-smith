using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-02-5f89a: the token a git host or tracker call authenticates with, read from the
/// entity's OWN auth secret at the moment of the call. Records carry the secret's name only,
/// so two instances of one host type each answer with their own token, and a secret re-pointed
/// in the store is read on the next call. A missing value throws
/// <see cref="Exceptions.MissingCredentialException"/>.
/// </summary>
public interface ICredentialResolver
{
    string For(RepoConnection repo);

    string For(ResolvedConnection connection);

    string For(TrackerConnection tracker);
}
