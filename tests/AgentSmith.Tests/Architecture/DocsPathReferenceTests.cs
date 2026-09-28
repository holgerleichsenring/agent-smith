using System.Text.RegularExpressions;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// A log line or a comment that says "see docs/…" sends the reader to a page. The docs tree
/// was reorganised and two such pointers kept naming pages that no longer existed, one of
/// them in the message an operator reads when a skill is refused.
/// </summary>
public sealed partial class DocsPathReferenceTests
{
    [GeneratedRegex(@"docs/[A-Za-z0-9_./-]+\.md")]
    private static partial Regex DocsPath();

    [Fact]
    public void SourceNamesNoDocsPathThatDoesNotExist() =>
        DeadReferences(SourceFiles()).Should().BeEmpty("every docs path the product names must be a page");

    [Fact]
    public void ExampleConfig_NamesNoDocsPathThatDoesNotExist() =>
        DeadReferences([ConfigSchemaFile.ExamplePath]).Should().BeEmpty(
            "the example configuration is where an operator starts reading");

    private static IEnumerable<string> SourceFiles() =>
        ArchitectureSources.HandWrittenBackendFiles().Concat(
            Directory.EnumerateFiles(
                    Path.Combine(ArchitectureSources.SourceRoot, "dashboard", "src"), "*.*", SearchOption.AllDirectories)
                .Where(p => p.EndsWith(".ts", StringComparison.Ordinal) || p.EndsWith(".tsx", StringComparison.Ordinal)));

    private static List<string> DeadReferences(IEnumerable<string> files) =>
    [
        .. files.SelectMany(file => DocsPath().Matches(File.ReadAllText(file))
                .Select(match => match.Value)
                .Where(path => !File.Exists(Path.Combine(ArchitectureSources.RepositoryRoot, path)))
                .Select(path => $"{Path.GetFileName(file)}: {path}"))
            .Distinct(),
    ];
}
