using System.Text.Json;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// 2026-08-26-04b6: a context carries what somebody decided and what the orchestrator acts
/// on. A READING — a value copied out of a file still in the repository — is discarded on the
/// way through, not refused: the prompt that asks for it ships behind a release and a pin, so
/// a model still offering one must not be punished for it.
/// </summary>
public sealed class ContextReadingsTests : IDisposable
{
    private const string Path = ".agentsmith/contexts/default/context.yaml";

    /// <summary>Every reading the typed document used to carry, plus the ones it never did.</summary>
    private const string WithReadings = """
        {
          "meta": {
            "workdir": ".", "project": "sample", "version": "1.0.0",
            "repo": "https://example.invalid/sample.git",
            "type": ["api"], "purpose": "Serves orders to the storefront."
          },
          "stack": {
            "lang": "C#", "image": "mcr.microsoft.com/dotnet/sdk:8.0", "runtime": ".NET 8",
            "frameworks": ["ASP.NET-Core"], "frontend": ["React"], "infra": ["Docker"],
            "testing": ["xUnit"], "ci": ["GitHub-Actions"], "sdks": ["SomeLib@12.3.0"]
          },
          "arch": {
            "style": ["Layered"], "patterns": ["Repository"], "layers": ["Domain"],
            "bounded-contexts": ["Ordering"], "hosting": "Generic Host"
          },
          "quality": {
            "lang": "english-only", "limits": { "method-lines": 20 },
            "principles": ["SOLID"]
          }
        }
        """;

    private static readonly string[] ReadingKeys =
    [
        "project:", "version:", "repo:", "runtime:", "frameworks:", "frontend:", "infra:",
        "testing:", "ci:", "sdks:", "style:", "patterns:", "layers:", "bounded-contexts:",
        "principles:",
    ];

    private readonly string _workDir = Directory.CreateTempSubdirectory("ctx-readings-").FullName;
    private readonly ContextYamlBuilders _builders = new();

    public void Dispose()
    {
        if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true);
    }

    [Fact]
    public async Task Write_ADocumentCarryingReadings_StoresThemNowhere()
    {
        await Write(WithReadings);

        var written = File.ReadAllText(System.IO.Path.Combine(_workDir, Path));
        foreach (var key in ReadingKeys)
            written.Should().NotContain(key, $"'{key.TrimEnd(':')}' is a reading");
    }

    [Fact]
    public async Task Write_ADocumentCarryingReadings_IsNotRefused() =>
        (await Write(WithReadings)).Should().StartWith("context.yaml written:",
            "the prompt that asks for the readings ships behind a release and a pin, so the "
            + "tool discards them instead of refusing the document that carries them");

    [Fact]
    public async Task Write_TheJudgementFields_AreStoredVerbatim()
    {
        await Write(WithReadings);

        Section("meta")["purpose"].Should().Be("Serves orders to the storefront.");
        ((List<object?>)Section("meta")["type"]!).Should().Equal("api");
        Section("stack")["image"].Should().Be("mcr.microsoft.com/dotnet/sdk:8.0");
        Section("quality")["lang"].Should().Be("english-only");
        Section("quality", "limits")["method-lines"].Should().Be("20");
        Section("arch")["hosting"].Should().Be("Generic Host",
            "a freeform key nobody classified a reading is somebody's own statement");
    }

    [Fact]
    public async Task State_IsUntouched()
    {
        var full = System.IO.Path.Combine(_workDir, Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "meta:\n  workdir: \".\"\nstate:\n  done:\n    p0001: \"shipped\"\n  active: {}\n");

        await Write(WithReadings);

        Section("state", "done")["p0001"].Should().Be("shipped",
            "the chronicle is the one section written by whoever made the call");
    }

    private async Task<string> Write(string document)
    {
        await using var sandbox = new InProcessSandbox(
            "job", _workDir, ownsWorkDir: false, NullLogger.Instance);
        var host = new WriteContextYamlToolHost(
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["repo"] = sandbox },
            defaultRepo: "repo",
            new ContextYamlSerializer(_builders),
            ContextGates.Build(),
            ContextGates.Writer());
        return await host.WriteContextYaml("repo", "default", JsonDocument.Parse(document).RootElement);
    }

    private Dictionary<object, object?> Section(params string[] keys)
    {
        var node = _builders.Deserializer.Deserialize<Dictionary<object, object?>>(
            File.ReadAllText(System.IO.Path.Combine(_workDir, Path)))!;
        foreach (var key in keys) node = (Dictionary<object, object?>)node[key]!;
        return node;
    }
}
