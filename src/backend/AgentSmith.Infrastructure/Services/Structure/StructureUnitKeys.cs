using System.Text.RegularExpressions;
using TreeSitter;

namespace AgentSmith.Infrastructure.Services.Structure;

/// <summary>
/// 2026-10-06-8565b: the key that identifies a unit within its file across commits — the
/// enclosing units and containers, its name, and its parameter types where the grammar has a
/// parameter list. Inserting an overload or reordering two impls leaves every other key alone;
/// only identical signatures fall back to an ordinal, which the collector assigns.
/// </summary>
public sealed partial class StructureUnitKeys(StructureLanguage language)
{
    public string Segment(Node? name, Node? parameters) =>
        parameters is null ? Text(name) : $"{Text(name)}({string.Join(", ", ParameterTypes(parameters))})";

    /// <summary>The segment a non-unit container contributes, or null when the node is none.</summary>
    public string? ContainerSegment(Node node)
    {
        if (!language.Containers.TryGetValue(node.Type, out var container)) return null;
        var parts = container.Fields.Select(node.GetChildForField).Where(n => n is not null).Select(Text);
        return string.Join(container.Separator, parts);
    }

    private static IEnumerable<string> ParameterTypes(Node parameters) =>
        parameters.NamedChildren
            .Where(p => !p.Type.Contains("comment", StringComparison.Ordinal))
            .Select(p => Text(p.GetChildForField("type") ?? p));

    private static string Text(Node? node) =>
        node is null ? string.Empty : Whitespace().Replace(node.Text, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
