namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-2d0c: what a meta file said at the base — its content, its absence, or a git that
/// could not answer (<paramref name="Failure"/>), which is never read as absence.
/// </summary>
public sealed record StructureBaseRead(string? Content, string? Failure = null)
{
    public static StructureBaseRead Absent { get; } = new((string?)null);

    public static StructureBaseRead Failed(string failure) => new(null, failure);
}
