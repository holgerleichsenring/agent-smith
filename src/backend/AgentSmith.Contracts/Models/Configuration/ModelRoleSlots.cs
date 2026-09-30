using AgentSmith.Contracts.Providers;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Every model role, primary first — one table, so the chain, the studio, the findings and
/// the preflight cannot name the roles differently. Roles that decide structure (primary,
/// planning, reasoning, context generation, code-map generation) need a strong model.
/// </summary>
public static class ModelRoleSlots
{
    public static IReadOnlyList<ModelRoleSlot> All { get; } =
    [
        new(TaskType.Primary, "primary", true, r => r.Primary, (r, v) => r.Primary = v),
        new(TaskType.Scout, "scout", false, r => r.Scout, (r, v) => r.Scout = v),
        new(TaskType.Planning, "planning", true, r => r.Planning, (r, v) => r.Planning = v),
        new(TaskType.Reasoning, "reasoning", true, r => r.Reasoning, (r, v) => r.Reasoning = v),
        new(TaskType.Summarization, "summarization", false, r => r.Summarization, (r, v) => r.Summarization = v),
        new(TaskType.ContextGeneration, "contextGeneration", true,
            r => r.ContextGeneration, (r, v) => r.ContextGeneration = v),
        new(TaskType.CodeMapGeneration, "codeMapGeneration", true,
            r => r.CodeMapGeneration, (r, v) => r.CodeMapGeneration = v),
    ];

    public static IReadOnlyList<string> Keys { get; } = [.. All.Select(s => s.Key)];

    public static ModelRoleSlot For(TaskType task) => All.Single(s => s.Task == task);

    public static ModelRoleSlot? Find(string key) => All.FirstOrDefault(s => s.Key == key);
}
