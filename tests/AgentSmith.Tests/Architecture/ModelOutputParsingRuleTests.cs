using System.Text.RegularExpressions;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0426: safety in the API, not in the discipline of the call site.
/// <para>
/// Fixing the one line that ended run 27 would have left nineteen more. A parser of MODEL
/// output must not read a number, a flag or a double without checking the value's KIND —
/// <c>JsonElement.TryGetInt32</c> reads like a safe accessor and THROWS on anything that
/// is not a number, and a model writing null for "I do not know" is normal.
/// </para>
/// <para>
/// The rule scans the parsers that read model answers. It permits a raw accessor on the
/// same line as an explicit kind check, because that is the safe form the reader itself
/// is built from.
/// </para>
/// </summary>
public sealed class ModelOutputParsingRuleTests
{
    private static readonly string[] ModelOutputParsers =
    [
        "Services/ProjectMapJsonReader.cs",
        "Services/LlmIntentParser.cs",
        "Services/Specs/SpecJsonReader.cs",
        "Services/Handlers/RawObservation.cs",
        "Services/Handlers/GateObservationParser.cs",
        "Services/Prompts/PlanJsonElementMapper.cs",
    ];

    private static readonly Regex RawAccessor = new(
        @"\.(TryGetInt32|TryGetInt64|GetInt32|GetInt64|GetDouble|GetBoolean)\s*\(",
        RegexOptions.Compiled);

    [Fact]
    public void NoModelOutputParser_ReadsANumberWithoutCheckingItsKind()
    {
        var violations = new List<string>();
        foreach (var relative in ModelOutputParsers)
        {
            var path = Path.Combine(ApplicationRoot(), relative);
            File.Exists(path).Should().BeTrue($"{relative} must exist for this rule to mean anything");
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!RawAccessor.IsMatch(lines[i])) continue;
                if (lines[i].Contains("JsonValueKind.")) continue;
                violations.Add($"{relative}:{i + 1} {lines[i].Trim()}");
            }
        }

        violations.Should().BeEmpty(
            "read model output through JsonValueReader — TryGetInt32 THROWS on a null, and "
            + "an analyzer answering file_count: null ended run 27 at step 12");
    }

    [Fact]
    public void Sanity_TheRuleWouldCatchTheLineThatEndedRun27()
        => RawAccessor.IsMatch(
                """FileCount: e.TryGetProperty("file_count", out var c) && c.TryGetInt32(out var i) ? i : 0,""")
            .Should().BeTrue();

    private static string ApplicationRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "backend")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the test must find the repository root");
        return Path.Combine(dir!.FullName, "src", "backend", "AgentSmith.Application");
    }
}
