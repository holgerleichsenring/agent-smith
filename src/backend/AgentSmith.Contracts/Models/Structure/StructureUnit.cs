namespace AgentSmith.Contracts.Models.Structure;

/// <summary>
/// 2026-10-06-8565b: one measured declaration. <paramref name="Key"/> is unique in its file
/// and stable when a sibling is inserted: the container chain, the name and — where the grammar
/// has one — the parameter-type list. Lines are 1-based and physical, from the first
/// non-attribute line to the last; nested units count inside their parent.
/// </summary>
public sealed record StructureUnit(StructureUnitKind Kind, string Key, int StartLine, int EndLine)
{
    public int Lines => EndLine - StartLine + 1;
}
