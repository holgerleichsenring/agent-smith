namespace AgentSmith.Infrastructure.Services.Structure;

/// <summary>
/// 2026-11-24-8756: which nodes of a grammar carry layout, as language data. A block is a node
/// whose first and last brace children open and close it; a member parent is a node whose named,
/// non-comment children each start a statement or member — what a formatter puts on a line of its
/// own. A section (a C# switch section) has a label, then statements: only its children whose kind
/// ends with one of <see cref="SectionMembers"/> are members, so the label shares their line.
/// </summary>
public sealed class StructureLayoutRules
{
    public List<string> Blocks { get; set; } = [];
    public List<string> MemberParents { get; set; } = [];
    public List<string> Sections { get; set; } = [];
    public List<string> SectionMembers { get; set; } = [];
}
