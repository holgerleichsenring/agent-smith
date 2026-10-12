using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Contracts.Services;
using TreeSitter;

namespace AgentSmith.Infrastructure.Services.Structure;

/// <summary>
/// 2026-10-06-8565b: <see cref="IStructureEngine"/> over tree-sitter. Each call gets its own
/// parser; grammar and query are shared, read-only, per language.
/// </summary>
public sealed class TreeSitterStructureEngine(
    StructureLanguageCatalog catalog, StructureGrammarLoader grammars, StructureUnitCollector collector)
    : IStructureEngine
{
    public StructureMeasurement Measure(string path, string content)
    {
        ArgumentNullException.ThrowIfNull(path);
        var (language, excluded) = catalog.For(path);
        if (language is null)
            return StructureMeasurement.NotMeasured($"no language claims '{Path.GetExtension(path)}'");
        if (excluded)
            return StructureMeasurement.NotMeasured($"{language.Name} excludes it as test or generated code");
        var grammar = grammars.For(language);
        if (grammar.Failure is { } failure) return StructureMeasurement.Unavailable(failure);

        using var parser = new Parser(grammar.Language!);
        using var tree = parser.Parse(content ?? string.Empty);
        if (tree is null) return StructureMeasurement.Unavailable($"{language.Name} parser returned no tree");
        var (units, types) = collector.Collect(tree.RootNode, grammar.Query!, language);
        return new StructureMeasurement(StructureOutcome.Measured, units, FileLines(content), types);
    }

    private static int FileLines(string? content) =>
        string.IsNullOrEmpty(content) ? 0 : content.Count(c => c == '\n') + (content.EndsWith('\n') ? 0 : 1);
}
