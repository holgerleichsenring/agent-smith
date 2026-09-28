using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// The studio's name for each role of the <c>models:</c> registry, and how to read and write
/// it — one table, so the catalog read and the patch write cannot name the roles differently.
/// The reserved role <c>coding</c> is the agent's own model and is not in it.
/// </summary>
internal static class StudioModelRoles
{
    private static readonly (string Role, Func<ModelRegistryConfig, ModelAssignment?> Get,
        Action<ModelRegistryConfig, ModelAssignment?> Set)[] Table =
    [
        ("scout", r => r.Scout, (r, v) => r.Scout = v),
        ("primary", r => r.Primary, (r, v) => r.Primary = v),
        ("planning", r => r.Planning, (r, v) => r.Planning = v),
        ("reasoning", r => r.Reasoning, (r, v) => r.Reasoning = v),
        ("summarization", r => r.Summarization, (r, v) => r.Summarization = v),
        ("contextGeneration", r => r.ContextGeneration, (r, v) => r.ContextGeneration = v),
        ("codeMapGeneration", r => r.CodeMapGeneration, (r, v) => r.CodeMapGeneration = v),
    ];

    public static IReadOnlyList<string> Names { get; } = [.. Table.Select(t => t.Role)];

    /// <summary>The roles the registry sets, under their studio names.</summary>
    public static IEnumerable<(string Role, ModelAssignment Assignment)> Of(ModelRegistryConfig registry) =>
        Table.Select(t => (t.Role, Assignment: t.Get(registry)))
            .Where(t => t.Assignment is not null)
            .Select(t => (t.Role, t.Assignment!));

    public static bool IsKnown(string role) => Names.Contains(role, StringComparer.Ordinal);

    public static ModelAssignment? Get(ModelRegistryConfig registry, string role) =>
        Table.Single(t => t.Role == role).Get(registry);

    public static void Set(ModelRegistryConfig registry, string role, ModelAssignment? value) =>
        Table.Single(t => t.Role == role).Set(registry, value);
}
