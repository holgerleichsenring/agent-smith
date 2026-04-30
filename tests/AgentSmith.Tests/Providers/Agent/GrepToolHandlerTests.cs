using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Agent;

public sealed class GrepToolHandlerTests : IDisposable
{
    private readonly string _repoPath;

    public GrepToolHandlerTests()
    {
        _repoPath = Path.Combine(Path.GetTempPath(), $"grep-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_repoPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoPath)) Directory.Delete(_repoPath, recursive: true);
    }

    [Fact]
    public void Grep_PatternMatchesLine_ReturnsMatchWithPathLineAndText()
    {
        File.WriteAllText(Path.Combine(_repoPath, "code.cs"),
            "namespace X;\npublic class Foo { }\n// TODO: refactor");

        var result = ParseMatches(InvokeGrep(new JsonObject { ["pattern"] = "TODO" }));

        result.Should().HaveCount(1);
        ((string)result[0]!["path"]!).Should().Be("code.cs");
        ((int)result[0]!["line"]!).Should().Be(3);
        ((string)result[0]!["text"]!).Should().Contain("TODO: refactor");
    }

    [Fact]
    public void Grep_GlobLimitsFiles_OnlyMatchingFilesAreSearched()
    {
        File.WriteAllText(Path.Combine(_repoPath, "code.cs"), "TODO in cs");
        File.WriteAllText(Path.Combine(_repoPath, "notes.md"), "TODO in markdown");

        var result = ParseMatches(InvokeGrep(new JsonObject
        {
            ["pattern"] = "TODO",
            ["glob"] = "**/*.cs"
        }));

        result.Should().HaveCount(1);
        ((string)result[0]!["path"]!).Should().Be("code.cs");
    }

    [Fact]
    public void Grep_NoMatches_ReturnsEmptyArray()
    {
        File.WriteAllText(Path.Combine(_repoPath, "a.txt"), "no match here");

        var json = JsonDocument.Parse(InvokeGrep(new JsonObject { ["pattern"] = "ZZZZ" }));

        json.RootElement.GetProperty("matches").GetArrayLength().Should().Be(0);
        json.RootElement.GetProperty("truncated").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void Grep_BoundedAt200Matches_TruncatedFlagSet()
    {
        var lines = string.Join('\n', Enumerable.Range(0, 250).Select(i => $"hit-{i}"));
        File.WriteAllText(Path.Combine(_repoPath, "many.txt"), lines);

        var json = JsonDocument.Parse(InvokeGrep(new JsonObject { ["pattern"] = "hit-" }));

        json.RootElement.GetProperty("matches").GetArrayLength().Should().Be(200);
        json.RootElement.GetProperty("truncated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Grep_ExcludesBuildAndGitDirectories()
    {
        var binDir = Path.Combine(_repoPath, "bin");
        Directory.CreateDirectory(binDir);
        File.WriteAllText(Path.Combine(binDir, "compiled.dll.txt"), "secret hit");
        File.WriteAllText(Path.Combine(_repoPath, "src.cs"), "secret hit");

        var result = ParseMatches(InvokeGrep(new JsonObject { ["pattern"] = "secret" }));

        result.Should().HaveCount(1);
        ((string)result[0]!["path"]!).Should().Be("src.cs");
    }

    [Fact]
    public void Grep_InvalidRegex_ReturnsErrorString()
    {
        var raw = InvokeGrep(new JsonObject { ["pattern"] = "[unclosed" });
        raw.Should().StartWith("Error:");
    }

    private string InvokeGrep(JsonObject input)
    {
        // GrepToolHandler is internal; reflect to instantiate.
        var asm = typeof(AgentSmith.Infrastructure.Models.ToolDefinitions).Assembly;
        var type = asm.GetType("AgentSmith.Infrastructure.Services.Providers.Agent.GrepToolHandler")!;
        var instance = Activator.CreateInstance(type, _repoPath, NullLogger.Instance)!;
        var method = type.GetMethod("Grep", BindingFlags.Public | BindingFlags.Instance)!;
        return (string)method.Invoke(instance, [input])!;
    }

    private static JsonArray ParseMatches(string raw)
    {
        var doc = JsonNode.Parse(raw)!;
        return (JsonArray)doc["matches"]!;
    }
}
