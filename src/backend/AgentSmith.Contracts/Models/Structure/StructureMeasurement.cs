namespace AgentSmith.Contracts.Models.Structure;

/// <summary>
/// 2026-10-06-8565b: one file's measurement. <paramref name="TypeCount"/> counts top-level
/// types only — a nested type belongs to the file's one type. <paramref name="Reason"/> says
/// why a file was not measured.
/// </summary>
public sealed record StructureMeasurement(
    StructureOutcome Outcome, IReadOnlyList<StructureUnit> Units, int FileLines, int TypeCount,
    string? Reason = null)
{
    public static StructureMeasurement NotMeasured(string reason) =>
        new(StructureOutcome.NotMeasured, [], 0, 0, reason);

    public static StructureMeasurement Unavailable(string reason) =>
        new(StructureOutcome.Unavailable, [], 0, 0, reason);
}
