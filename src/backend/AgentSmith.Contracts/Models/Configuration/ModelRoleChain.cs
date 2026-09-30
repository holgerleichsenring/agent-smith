using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Which model answers a task for one agent — the fallback chain, stated once so the
/// runtime, the run gate and the startup report cannot disagree about it. A role is set when
/// it names a catalog entry (<c>use:</c>, which wins over any inline field beside it) or an
/// inline model. An unset role inherits: primary from the agent's own
/// <c>model</c>/<c>deployment</c>, every other role from primary, and code-map generation
/// from scout before primary. An inheriting role takes the assignment it inherits whole.
/// </summary>
public sealed class ModelRoleChain(AgentConfig agent)
{
    /// <summary>The roles that carried a built-in Claude model before the chain existed. A
    /// models block that leaves one of them unset now inherits where it used to get Claude.</summary>
    private static readonly TaskType[] FormerlyDefaulted =
        [TaskType.Primary, TaskType.Scout, TaskType.Planning, TaskType.Summarization];

    /// <summary>The assignment the task answers with; throws when the role it reaches names
    /// a catalog entry the agent does not declare.</summary>
    public ModelAssignment For(TaskType task) => TryFor(task) ?? throw UnknownEntry(SetSlot(task)!);

    /// <summary>The assignment the task answers with, or null when the role it reaches names a
    /// catalog entry the agent does not declare.</summary>
    public ModelAssignment? TryFor(TaskType task)
    {
        if (SetSlot(task) is not { } slot) return AgentFallback();
        var assignment = Declared(slot)!;
        if (string.IsNullOrWhiteSpace(assignment.Use)) return assignment;
        return agent.Catalog.TryGetValue(assignment.Use, out var entry) ? entry.ToAssignment() : null;
    }

    /// <summary>The model primary answers on, for display and for every fallback that once read
    /// <c>agent.model</c>; empty when none is named or primary's entry is undeclared.</summary>
    public string PrimaryModel => TryFor(TaskType.Primary)?.Model ?? string.Empty;

    /// <summary>The catalog entry the task resolves to, or null when it answers with an
    /// inline role or the agent's own model.</summary>
    public string? EntryFor(TaskType task) =>
        SetSlot(task) is { } slot && Declared(slot)!.Use is { } use && !string.IsNullOrWhiteSpace(use) ? use : null;

    /// <summary>The roles a declared <c>models:</c> block leaves to inherit that once carried a
    /// built-in Claude model — empty when there is no block, which always inherited everything.</summary>
    public IReadOnlyList<string> InheritingFormerDefaults() =>
        agent.Models is null
            ? []
            : [.. FormerlyDefaulted.Select(ModelRoleSlots.For).Where(s => !IsSet(Declared(s))).Select(s => s.Key)];

    /// <summary>Every role whose <c>use:</c> names an entry the catalog does not declare.</summary>
    public IReadOnlyList<(string Role, string Use)> UnknownUses() =>
        [.. ModelRoleSlots.All
            .Select(s => (Role: s.Key, Use: Declared(s)?.Use))
            .Where(r => !string.IsNullOrWhiteSpace(r.Use) && !agent.Catalog.ContainsKey(r.Use!))
            .Select(r => (r.Role, r.Use!))];

    private ModelRoleSlot? SetSlot(TaskType task) => Order(task).FirstOrDefault(s => IsSet(Declared(s)));

    private static IEnumerable<ModelRoleSlot> Order(TaskType task) => task switch
    {
        TaskType.Primary => [ModelRoleSlots.For(TaskType.Primary)],
        TaskType.CodeMapGeneration =>
        [
            ModelRoleSlots.For(TaskType.CodeMapGeneration), ModelRoleSlots.For(TaskType.Scout),
            ModelRoleSlots.For(TaskType.Primary),
        ],
        _ when Enum.IsDefined(task) => [ModelRoleSlots.For(task), ModelRoleSlots.For(TaskType.Primary)],
        _ => [ModelRoleSlots.For(TaskType.Primary)],
    };

    private ModelAssignment? Declared(ModelRoleSlot slot) => agent.Models is { } models ? slot.Get(models) : null;

    /// <summary>A role counts as set when it names a catalog entry or a model.</summary>
    private static bool IsSet(ModelAssignment? assignment) =>
        !string.IsNullOrWhiteSpace(assignment?.Use) || !string.IsNullOrWhiteSpace(assignment?.Model);

    private ConfigurationException UnknownEntry(ModelRoleSlot slot) =>
        new($"Model role '{slot.Key}' uses catalog entry '{Declared(slot)!.Use}', which the agent's catalog "
            + $"does not declare (declared: {string.Join(", ", agent.Catalog.Keys)}). Name a declared entry "
            + $"under models.{slot.Key}.use.");

    private ModelAssignment AgentFallback()
    {
        var primary = agent.Models?.Primary;
        return new ModelAssignment
        {
            Model = agent.Model,
            Deployment = primary?.Deployment ?? agent.Deployment,
            MaxTokens = primary?.MaxTokens ?? new ModelAssignment().MaxTokens,
            ContextWindowTokens = primary?.ContextWindowTokens,
        };
    }
}
