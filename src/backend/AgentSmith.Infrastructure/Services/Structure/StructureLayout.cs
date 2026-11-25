using TreeSitter;

namespace AgentSmith.Infrastructure.Services.Structure;

/// <summary>
/// 2026-11-24-8756: the line breaks a formatter would add inside a unit, so a size counted in
/// lines cannot be lowered by joining statements onto one line. The layout events are a member's
/// start and a block's two braces. On each physical line, every gap between two consecutive
/// events is one break, except before an opening brace (it stays on the line of what it opens)
/// and inside a compact block — one on a single line holding at most one member in its whole
/// subtree (`{ }`, `{ return x; }`), which formatters keep. Code with one statement per line has
/// no break.
/// </summary>
public sealed class StructureLayout
{
    private enum Event { Member, Open, Close }

    private sealed record Mark(int Row, int Column, Event Kind, int Block);

    public int Breaks(Node unit, StructureLayoutRules? rules)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (rules is null || (rules.Blocks.Count == 0 && rules.MemberParents.Count == 0 && rules.Sections.Count == 0))
            return 0;
        var marks = new List<Mark>();
        var compact = new HashSet<int>();
        Collect(unit, rules, marks, compact);
        return marks.GroupBy(m => m.Row).Sum(line => Gaps([.. line.OrderBy(m => m.Column)], compact));
    }

    private static int Gaps(IReadOnlyList<Mark> line, HashSet<int> compact)
    {
        var breaks = 0;
        for (var i = 1; i < line.Count; i++)
        {
            var (before, after) = (line[i - 1], line[i]);
            var kept = after.Kind == Event.Open
                       || (before.Kind == Event.Open && compact.Contains(before.Block))
                       || (after.Kind == Event.Close && compact.Contains(after.Block));
            if (!kept) breaks++;
        }
        return breaks;
    }

    // Returns how many members the subtree holds, so a block knows whether it is compact.
    private static int Collect(Node node, StructureLayoutRules rules, List<Mark> marks, HashSet<int> compact)
    {
        var children = node.Children.ToList();
        var (open, close) = Braces(node, children, rules);
        var members = 0;
        for (var i = 0; i < children.Count; i++)
        {
            if (IsMember(node, children[i], rules) && (open < 0 || (i > open && i < close)))
            {
                marks.Add(At(children[i], Event.Member, node.StartIndex));
                members++;
            }
            members += Collect(children[i], rules, marks, compact);
        }
        if (open < 0) return members;
        marks.Add(At(children[open], Event.Open, node.StartIndex));
        marks.Add(At(children[close], Event.Close, node.StartIndex));
        if (members <= 1 && children[open].StartPosition.Row == children[close].StartPosition.Row)
            compact.Add(node.StartIndex);
        return members;
    }

    private static (int Open, int Close) Braces(Node node, List<Node> children, StructureLayoutRules rules)
    {
        if (!rules.Blocks.Contains(node.Type)) return (-1, -1);
        var open = children.FindIndex(c => c.Type == "{");
        var close = children.FindLastIndex(c => c.Type == "}");
        return open >= 0 && close > open ? (open, close) : (-1, -1);
    }

    private static bool IsMember(Node parent, Node child, StructureLayoutRules rules)
    {
        if (!child.IsNamed || child.Type.Contains("comment", StringComparison.Ordinal)) return false;
        if (rules.MemberParents.Contains(parent.Type)) return true;
        return rules.Sections.Contains(parent.Type)
               && rules.SectionMembers.Exists(suffix => child.Type.EndsWith(suffix, StringComparison.Ordinal));
    }

    private static Mark At(Node node, Event kind, int block) =>
        new(node.StartPosition.Row, node.StartPosition.Column, kind, block);
}
