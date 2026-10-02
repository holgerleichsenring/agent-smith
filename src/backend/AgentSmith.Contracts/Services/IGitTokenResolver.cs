using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-02-5f89g: the credential a repo's clone, fetch and push use — its OWN auth secret, so
/// repos of two instances of one host type can sit in one run. A local repo gets
/// <see cref="GitCredential.None"/> and is never looked up; a remote repo whose secret holds
/// nothing throws <see cref="Exceptions.MissingCredentialException"/> naming the secret.
/// </summary>
public interface IGitTokenResolver
{
    GitCredential For(RepoConnection repo);
}
