using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-09-01-379a: the <c>probe:</c> block a repository writes has to survive every hop
/// between the file and the step that asks the target.
/// <para>
/// Driven through the REAL parser and the REAL resolver rather than a hand-built record,
/// for the reason 2026-08-28-7b41 records: a field parsed and then dropped at its
/// construction sites shipped green for two releases because every test built the record by
/// hand. The read shape is where an undeclared key dies without a warning.
/// </para>
/// </summary>
public sealed class ContextYamlProbeBlockTests
{
    private const string Yaml = """
        meta:
          workdir: "warehouse"
        stack:
          lang: "python"
          image: "python:3.12-bookworm"
        probe:
          target: "the warehouse dev workspace"
          command: "sf org display --target-org devhub"
        """;

    [Fact]
    public async Task ContextYaml_AProbeBlock_ReachesTheDiscoveryFromRealYaml()
    {
        var discoveries = await Resolver().ResolveAllAsync(Source, CancellationToken.None);

        var probe = discoveries.Should().ContainSingle().Which.Probe;
        probe.Should().NotBeNull(
            "the read shape drops an undeclared key silently — the parser is the hop this "
            + "field dies in, so it is driven from real YAML");
        probe!.Target.Should().Be("the warehouse dev workspace");
        probe.Command.Should().Be("sf org display --target-org devhub");
    }

    /// <summary>p0261 `--context NAME` is the discovery's second production construction
    /// site, and the one 2026-08-28-7b41 found dropping a field.</summary>
    [Fact]
    public async Task ContextYaml_AProbeBlock_ReachesTheExplicitContextPathToo()
    {
        var discoveries = await Resolver()
            .ResolveContextAsync(Source, "warehouse", CancellationToken.None);

        discoveries.Should().ContainSingle().Which.Probe.Should().NotBeNull(
            "the pinned-context path builds the same record");
    }

    [Fact]
    public void ContextYaml_AProbeWithoutATarget_IsNotAProbe()
    {
        var summary = new ContextYamlSerializer(new ContextYamlBuilders()).Parse("""
            meta:
              workdir: "."
            probe:
              command: "kubectl auth whoami"
            """).Summary;

        summary!.Probe.Should().BeNull(
            "a probe with no target has nothing to name in its own failure");
    }

    [Fact]
    public async Task ContextYaml_AProbeBlock_ReachesTheResolverFromItsOwnDeclaration()
    {
        var discoveries = await Resolver().ResolveAllAsync(Source, CancellationToken.None);
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
            {
                ["repo"] = discoveries,
            });

        var resolved = new ContextTargetProbeResolver().For(pipeline, "repo");

        // 2026-09-03-7bac: the declaration that is asked is still this context's own —
        // the representative-vs-list distinction the resolver exists for. Where it is
        // asked FROM is no longer read off meta.workdir: the probe runs at the repository
        // root like every other command, and one needing another directory says so.
        resolved.Should().ContainSingle().Which.Target.Should().Be("the warehouse dev workspace",
            "each declaration is asked with its own command, not the representative's");
    }

    /// <summary>
    /// The tool description asks a model for the block, so the write path has to emit one
    /// the reader gets back — the schema refuses what it does not declare, and the reader
    /// drops what its shape does not name.
    /// </summary>
    [Fact]
    public void ContextYaml_AProbeBlockTheModelWrites_SurvivesTheWritePath()
    {
        var gate = TestHelpers.ContextGates.Build();
        gate.TryRead(System.Text.Json.JsonDocument.Parse("""
            {
              "meta": { "workdir": "." },
              "stack": { "lang": "python", "image": "python:3.12-bookworm" },
              "probe": { "target": "the staging cluster", "command": "kubectl auth whoami" }
            }
            """).RootElement, out var typed, out var defect).Should().BeTrue("{0}", defect);
        gate.Defect(typed!).Should().BeNull("the schema declares the block the tool asks for");

        var yaml = TestHelpers.ContextGates.Serializer().Serialize(typed!);

        Architecture.ContextSchemaFile.Validate(yaml).Should().BeEmpty();
        new ContextYamlSerializer(new ContextYamlBuilders()).Parse(yaml).Summary!.Probe
            .Should().Be(typed!.Probe,
                "what the writer emits is what the reader reads back — the one-builder rule");
    }

    /// <summary>
    /// The discoveries are checkpointed at <see cref="ContextKeys.RemoteContextInventory"/>
    /// and restored on resume, so a field that cannot round-trip is a target a resumed run
    /// never asks.
    /// </summary>
    [Fact]
    public async Task ContextYaml_AProbeBlock_SurvivesTheResumeRoundTrip()
    {
        var discoveries = await Resolver().ResolveAllAsync(Source, CancellationToken.None);
        var before = new PipelineContext();
        before.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.RemoteContextInventory,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>> { ["repo"] = discoveries });
        var serializer = new PipelineContextSerializer(NullLogger<PipelineContextSerializer>.Instance);

        var after = new PipelineContext();
        serializer.Restore(serializer.Serialize(before), after);

        after.TryGet<Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.RemoteContextInventory, out var restored).Should().BeTrue();
        restored!["repo"].Single().Probe.Should().Be(discoveries.Single().Probe,
            "a resumed run asks the same target the first attempt read");
    }

    private static readonly RepoConnection Source = new() { Url = "https://example.invalid/repo.git" };

    private static SandboxLanguageResolver Resolver()
    {
        var source = new Mock<ISourceProvider>();
        source.Setup(p => p.ListDirectoryAsync(".agentsmith/contexts", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "warehouse" });
        source.Setup(p => p.TryReadFileAsync(
                ".agentsmith/contexts/warehouse/context.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Yaml);
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(source.Object);
        return new SandboxLanguageResolver(
            factory.Object,
            new ContextYamlParser(new ContextYamlSerializer(new ContextYamlBuilders())),
            NullLogger<SandboxLanguageResolver>.Instance);
    }
}
