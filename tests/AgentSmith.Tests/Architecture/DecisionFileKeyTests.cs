using System.Text.RegularExpressions;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-10-06-03c7b: decision.schema.json requires one of <c>spec:</c> or <c>run:</c>; the
/// migration renamed the key in every file, and nothing may bring <c>phase:</c> back.
/// </summary>
public sealed class DecisionFileKeyTests
{
    private static readonly Regex Owner = new(@"^(?:spec|run):", RegexOptions.Multiline);
    private static readonly Regex Legacy = new(@"^phase:", RegexOptions.Multiline);

    [Fact]
    public void DecisionFiles_EveryOne_StartsWithSpecOrRun()
    {
        var offenders = Directory
            .EnumerateFiles(Path.Combine(ArchitectureSources.AgentSmithRoot, "decisions"), "*.yaml")
            .Where(path => File.ReadAllText(path) is var text && (!Owner.IsMatch(text) || Legacy.IsMatch(text)))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty("a decision file names its owner under spec: or run:, never phase:");
    }
}
