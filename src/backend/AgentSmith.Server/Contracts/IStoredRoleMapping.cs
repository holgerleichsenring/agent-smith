using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Contracts;

/// <summary>
/// 2026-08-25-1806: the role mapping as the config store currently holds it, asked once per
/// request. One method, because role resolution needs exactly one thing of the store and an
/// authorization path that could reach the rest of it is a wider door than it needs.
/// </summary>
public interface IStoredRoleMapping
{
    /// <summary>
    /// The stored mapping, or <c>null</c> when the store cannot answer. A store that is
    /// unreadable must not resolve every caller to no roles at all: the caller of this
    /// falls back to the bootstrap seed, which is the mapping the installation had before.
    /// </summary>
    RoleMappingConfig? Read();
}
