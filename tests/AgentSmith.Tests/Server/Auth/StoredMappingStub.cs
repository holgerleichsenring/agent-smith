using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-08-25-1806: the config store's answer, stated rather than persisted. The production
/// reader hands back the same instance until a write reassembles the store's document, so a
/// test that means "a save happened" assigns a NEW instance — which is the same signal the
/// real store gives and the same one the source caches against.
/// </summary>
internal sealed class StoredMappingStub(RoleMappingConfig? mapping) : IStoredRoleMapping
{
    public RoleMappingConfig? Stored { get; set; } = mapping;

    public RoleMappingConfig? Read() => Stored;
}
