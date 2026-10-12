using AgentSmith.Contracts.Models.Structure;
using TreeSitter;

namespace AgentSmith.Infrastructure.Services.Structure;

/// <summary>2026-10-06-8565b: one @type or @function match with the nodes its key is built from.</summary>
public sealed record StructureCapture(StructureUnitKind Kind, Node Node, Node? Name, Node? Parameters)
{
    public (int Start, int End) Span => (Node.StartIndex, Node.EndIndex);
}
