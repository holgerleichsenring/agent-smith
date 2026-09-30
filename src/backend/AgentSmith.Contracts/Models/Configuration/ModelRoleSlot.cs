using AgentSmith.Contracts.Providers;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// One role of the <c>models:</c> registry: the task it answers, its wire key (the camelCased
/// task name the studio, the findings and the preflight all use), whether it decides structure
/// and so needs a strong model, and how to read and write it on a registry.
/// </summary>
public sealed record ModelRoleSlot(
    TaskType Task,
    string Key,
    bool NeedsStrong,
    Func<ModelRegistryConfig, ModelAssignment?> Get,
    Action<ModelRegistryConfig, ModelAssignment?> Set);
