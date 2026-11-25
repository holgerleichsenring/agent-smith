namespace AgentSmith.Contracts.Models.Structure;

/// <summary>
/// 2026-10-06-8565b: one measured declaration. <paramref name="Key"/> is unique in its file
/// and stable when a sibling is inserted: the container chain, the name and — where the grammar
/// has one — the parameter-type list. Lines are 1-based and physical, from the first
/// non-attribute line to the last; nested units count inside their parent.
/// 2026-11-24-8756: <paramref name="Breaks"/> are the line breaks a formatter would add — one per
/// statement or brace joined onto another's line — and <see cref="Lines"/> counts them, so joining
/// lines never shrinks a unit.
/// </summary>
public sealed record StructureUnit(StructureUnitKind Kind, string Key, int StartLine, int EndLine, int Breaks = 0)
{
    public int PhysicalLines => EndLine - StartLine + 1;

    public int Lines => PhysicalLines + Breaks;
}
