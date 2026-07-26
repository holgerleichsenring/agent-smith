using System.Text.Json;
using System.Text.RegularExpressions;
using AgentSmith.Application.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Memory;

/// <summary>
/// p0380: the decision.schema.json `run` slot was dead by construction — its
/// pattern (^r[0-9a-z]+$) rejected every real p0156 run id. Pin that the
/// widened pattern accepts what RunIdGenerator actually produces.
/// </summary>
public sealed class DecisionSchemaTests
{
    [Fact]
    public void DecisionSchema_RunPattern_AcceptsP0156RunIds()
    {
        var pattern = ReadRunPattern();

        Regex.IsMatch(RunIdGenerator.Generate(DateTimeOffset.UtcNow), pattern).Should().BeTrue(
            "the run slot must accept the canonical run id format");
        Regex.IsMatch("2026-05-20T22-27-43-8a3f", pattern).Should().BeTrue();
        Regex.IsMatch("r07", pattern).Should().BeTrue("legacy short ids stay accepted");
        Regex.IsMatch("not a run id", pattern).Should().BeFalse();
        Regex.IsMatch("2026-05-20T22-27-43-XYZW", pattern).Should().BeFalse();
    }

    private static string ReadRunPattern()
    {
        var schemaPath = FindUpward(Path.Combine(".agentsmith", "decision.schema.json"));
        using var doc = JsonDocument.Parse(File.ReadAllText(schemaPath));
        return doc.RootElement
            .GetProperty("properties").GetProperty("run").GetProperty("pattern").GetString()!;
    }

    private static string FindUpward(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"Could not locate {relative} above {AppContext.BaseDirectory}");
    }
}
