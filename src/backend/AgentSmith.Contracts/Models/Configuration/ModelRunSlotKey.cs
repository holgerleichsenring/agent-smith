using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-20-24bd: the deployment a run's places are counted on — the agent's PRIMARY assignment as provider type,
/// effective endpoint and effective deployment (<see cref="ModelAssignment.EffectiveDeployment"/>, the name the
/// builder sends). Built here and nowhere else, so the gate, the store and a test cannot count one deployment under
/// two keys. A key longer than the stored column keeps its readable head and ends in the SHA-256 of the whole.
/// </summary>
public static class ModelRunSlotKey
{
    /// <summary>The stored column's length.</summary>
    public const int MaxLength = 191; // not a limit: the width of the indexed column the key is stored in

    private const int ReadableHead = 120; // not a limit: how much of a long key stays readable before its hash

    /// <summary>The key of <paramref name="agent"/>'s primary deployment.</summary>
    public static string For(AgentConfig agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        var primary = Primary(agent);
        var type = (primary.ProviderType ?? agent.Type).Trim().ToLowerInvariant();
        var endpoint = (primary.EffectiveEndpoint(agent) ?? string.Empty).Trim().TrimEnd('/').ToLowerInvariant();
        var key = $"{type}|{endpoint}|{primary.EffectiveDeployment(agent).Trim()}";
        if (key.Length <= MaxLength) return key;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return $"{key[..ReadableHead]}#{hash}";
    }

    /// <summary>The deployment's name as a person reads it — the same name the key carries.</summary>
    public static string Model(AgentConfig agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return Primary(agent).EffectiveDeployment(agent);
    }

    private static ModelAssignment Primary(AgentConfig agent) =>
        new ModelRoleChain(agent).TryFor(TaskType.Primary) ?? new ModelAssignment { Model = agent.Model };
}
