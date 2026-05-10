namespace AgentSmith.Domain.Entities;

/// <summary>
/// Files and modules a Plan declares as its scope. Populated only on the strict
/// (schema-validated) path; legacy lax-parsed plans leave both lists empty.
/// </summary>
public sealed record PlanScope(
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Modules)
{
    public static PlanScope Empty { get; } =
        new(Array.Empty<string>(), Array.Empty<string>());
}
