using AgentSmith.Contracts.Models.Structure;
using TreeSitter;

namespace AgentSmith.Infrastructure.Services.Structure;

/// <summary>
/// 2026-10-06-8565b: turns a language query's captures into measured units. A unit inside a
/// @test region is not measured; a type nested in another type does not count toward the
/// file's type count; lines run from the first non-attribute line to the last.
/// </summary>
public sealed class StructureUnitCollector
{
    public (IReadOnlyList<StructureUnit> Units, int TypeCount) Collect(
        Node root, Query query, StructureLanguage language)
    {
        var (captures, tests) = Capture(root, query);
        var kept = captures.Where(c => !tests.Any(t => Inside(c.Span, t))).ToList();
        var keys = new StructureUnitKeys(language);
        var segments = new Dictionary<Node, string>();
        foreach (var capture in kept)
            segments.TryAdd(capture.Node, keys.Segment(capture.Name, capture.Parameters));
        var unique = Deduplicate([.. kept.Select(c => Chain(c, segments, keys))]);
        var units = kept.Select((c, i) => new StructureUnit(
            c.Kind, unique[i], StartLine(c.Node, language), c.Node.EndPosition.Row + 1));
        var types = kept.Where(c => c.Kind == StructureUnitKind.Type).Select(c => c.Node).ToHashSet();
        var topLevel = kept.Count(c => c.Kind == StructureUnitKind.Type && !HasAncestor(c.Node, types));
        return ([.. units], topLevel);
    }

    private static (List<StructureCapture> Units, List<(int, int)> Tests) Capture(Node root, Query query)
    {
        var units = new List<StructureCapture>();
        var tests = new List<(int, int)>();
        using var cursor = query.Execute(root);
        foreach (var match in cursor.Matches)
        {
            Node? Named(string name) => match.Captures.FirstOrDefault(c => c.Name == name)?.Node;
            if (Named("test") is { } test) tests.Add((test.StartIndex, test.EndIndex));
            else if (Named("type") is { } type) units.Add(new(StructureUnitKind.Type, type, Named("name"), null));
            else if (Named("function") is { } fn)
                units.Add(new(StructureUnitKind.Function, fn, Named("name"), Named("params")));
        }
        return (units, tests);
    }

    private static string Chain(
        StructureCapture capture, Dictionary<Node, string> segments, StructureUnitKeys keys)
    {
        var chain = new List<string> { segments[capture.Node] };
        for (var node = capture.Node.Parent; node is not null; node = node.Parent)
        {
            var segment = segments.GetValueOrDefault(node) ?? keys.ContainerSegment(node);
            if (!string.IsNullOrEmpty(segment)) chain.Insert(0, segment);
        }
        return string.Join(".", chain);
    }

    private static bool HasAncestor(Node node, HashSet<Node> types)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
            if (types.Contains(parent)) return true;
        return false;
    }

    // Identical signatures in one file keep the first key bare and number the rest in source order.
    private static IReadOnlyList<string> Deduplicate(IReadOnlyList<string> keys)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        return [.. keys.Select(k => (seen[k] = seen.GetValueOrDefault(k) + 1) == 1 ? k : $"{k}#{seen[k]}")];
    }

    private static bool Inside((int Start, int End) span, (int Start, int End) region) =>
        span.Start >= region.Start && span.End <= region.End;

    private static int StartLine(Node node, StructureLanguage language) =>
        (node.Children.FirstOrDefault(c => !language.AttributeKinds.Contains(c.Type)
                                           && !c.Type.Contains("comment", StringComparison.Ordinal)) ?? node)
            .StartPosition.Row + 1;
}
