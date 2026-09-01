using System.Text.Json;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

using AgentSmith.Tests.Architecture;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// 2026-08-26-364f: the context write is a read-modify-write, so the sections the typed
/// ContextYamlDocument does not model survive it. Every case runs against a REAL sandbox
/// on a real temp directory — "left on disk" is only checkable where a disk exists.
/// </summary>
[Collection(ExternalProcessCollection.Name)]
public sealed class ContextWritePreservationTests : IDisposable
{
    private const string Path = ".agentsmith/contexts/default/context.yaml";

    private const string Existing = """
                                    meta:
                                      workdir: "."
                                      project: "sample"
                                    methodology:
                                      version: "2.4.0"
                                    stack:
                                      lang: "C#"
                                      image: "old-image:1"
                                      sdks:
                                        - "SomeLib@12.3.0"
                                    arch:
                                      style:
                                        - "Layered"
                                    integrations:
                                      bus:
                                        type: "queue"
                                        does: "moves work between services"
                                    data:
                                      primary:
                                        engine: "postgres"
                                    state:
                                      done:
                                        p0001: "shipped the first thing -> .agentsmith/phases/done/p0001.yaml"
                                      active: {}
                                    decisions:
                                      - id: "ADR-1"
                                        title: "one file per decision"
                                        reason: "a reader learns the application from one file"
                                    """;

    private const string Document = """
                                    {
                                      "meta": { "workdir": ".", "project": "sample" },
                                      "stack": { "lang": "C#", "image": "mcr.microsoft.com/dotnet/sdk:8.0" }
                                    }
                                    """;

    private readonly string _workDir = Directory.CreateTempSubdirectory("ctx-write-").FullName;
    private readonly ContextYamlBuilders _builders = new();

    public void Dispose()
    {
        if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true);
    }

    [Fact]
    public async Task Write_AnExistingContextWithState_KeepsStateVerbatim()
    {
        Seed(Existing);

        (await Write()).Should().StartWith("context.yaml written:");

        Section("state", "done").Should().ContainKey("p0001")
            .WhoseValue.Should().Be("shipped the first thing -> .agentsmith/phases/done/p0001.yaml");
    }

    [Fact]
    public async Task Write_AnExistingContextWithMethodologyIntegrationsData_KeepsThemAll()
    {
        Seed(Existing);

        await Write();

        Section("methodology")["version"].Should().Be("2.4.0");
        Section("integrations", "bus")["does"].Should().Be("moves work between services");
        Section("data", "primary")["engine"].Should().Be("postgres");
    }

    [Fact]
    public async Task Write_AnExistingContextWithDecisions_KeepsThem()
    {
        Seed(Existing);

        await Write();

        var decisions = (List<object?>)Root()["decisions"]!;
        decisions.Should().HaveCount(1);
        ((Dictionary<object, object?>)decisions[0]!)["id"].Should().Be("ADR-1");
    }

    [Fact]
    public async Task Write_AModelledSection_IsReplacedNotMerged()
    {
        Seed(Existing);

        await Write();

        var stack = Section("stack");
        stack["image"].Should().Be("mcr.microsoft.com/dotnet/sdk:8.0");
        stack.Should().NotContainKey("sdks",
            "a section the document states is stated afresh, not merged into the old one");
    }

    [Fact]
    public async Task Write_ASectionTheDocumentOmits_IsLeftAsItWas()
    {
        Seed(Existing);

        await Write();

        Section("arch").Should().ContainKey("style");
    }

    [Fact]
    public async Task Write_NoFileYet_WritesTheTypedDocumentAlone()
    {
        (await Write()).Should().StartWith("context.yaml written:");

        var root = Root();
        Section("meta")["workdir"].Should().Be(".");
        root.Keys.Select(k => k.ToString()).Should().BeEquivalentTo("meta", "stack");
    }

    [Fact]
    public async Task Write_AnUnparseableFile_IsRefusedAndNamesTheParseError()
    {
        Seed("meta:\n  workdir: \".\"\n   project: broken indent\n");

        var result = await Write();

        result.Should().StartWith("Error:");
        result.Should().Contain(Path);
        result.Should().Contain("Line:", "the refusal carries the parse error, not a shrug");
    }

    [Fact]
    public async Task Write_AnUnparseableFile_LeavesItOnDisk()
    {
        const string broken = "meta:\n  workdir: \".\"\n   project: broken indent\n";
        Seed(broken);

        await Write();

        File.ReadAllText(System.IO.Path.Combine(_workDir, Path)).Should().Be(broken);
    }

    [Fact]
    public async Task ReInit_AContextWithAChronicle_StillHasItAfterwards()
    {
        Seed(Existing);

        await Write();
        await Write();

        Section("state", "done").Should().ContainKey("p0001");
        Section("methodology")["version"].Should().Be("2.4.0");
    }

    private void Seed(string yaml)
    {
        var full = System.IO.Path.Combine(_workDir, Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, yaml);
    }

    private async Task<string> Write()
    {
        await using var sandbox = new InProcessSandbox(
            "job", _workDir, ownsWorkDir: false, NullLogger.Instance);
        var host = new WriteContextYamlToolHost(
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["repo"] = sandbox },
            defaultRepo: "repo",
            new ContextYamlSerializer(_builders),
            ContextGates.Build(),
            ContextGates.Writer(),
            ContextGates.DerivationStamp());
        return await host.WriteContextYaml("repo", "default", JsonDocument.Parse(Document).RootElement);
    }

    private Dictionary<object, object?> Root() =>
        _builders.Deserializer.Deserialize<Dictionary<object, object?>>(
            File.ReadAllText(System.IO.Path.Combine(_workDir, Path)))!;

    private Dictionary<object, object?> Section(params string[] keys)
    {
        var node = Root();
        foreach (var key in keys) node = (Dictionary<object, object?>)node[key]!;
        return node;
    }
}
