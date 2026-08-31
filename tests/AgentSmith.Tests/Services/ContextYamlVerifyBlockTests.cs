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
/// 2026-08-31-26d4: the <c>verify:</c> block a repository writes has to survive every hop
/// between the file and the gate.
/// <para>
/// It is driven through the REAL parser and the REAL resolver rather than a hand-built
/// record, because a hand-built record is exactly what let <c>meta.domain</c> ship green
/// for two releases while both production construction sites dropped it (2026-08-28-7b41).
/// The read shape is where an undeclared key dies without a warning; a producer test is
/// the only kind that notices.
/// </para>
/// </summary>
public sealed class ContextYamlVerifyBlockTests
{
    private const string Yaml = """
        meta:
          workdir: "warehouse"
        stack:
          lang: "python"
          image: "python:3.12-bookworm"
        verify:
          - label: "build"
            command: "dbt compile"
          - label: "bundle"
            command: "databricks bundle validate"
            when_present: "databricks.yml"
        """;

    private static SandboxLanguageResolver Resolver(out Mock<ISourceProvider> provider)
    {
        var source = new Mock<ISourceProvider>();
        source.Setup(p => p.ListDirectoryAsync(".agentsmith/contexts", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "warehouse" });
        source.Setup(p => p.TryReadFileAsync(
                ".agentsmith/contexts/warehouse/context.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Yaml);
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(source.Object);
        provider = source;
        return new SandboxLanguageResolver(
            factory.Object,
            new ContextYamlParser(new ContextYamlSerializer(new ContextYamlBuilders())),
            NullLogger<SandboxLanguageResolver>.Instance);
    }

    private static readonly RepoConnection Source = new() { Url = "https://example.invalid/repo.git" };

    [Fact]
    public async Task ContextYaml_AVerifyBlock_ReachesTheDiscoveryFromRealYaml()
    {
        var discoveries = await Resolver(out _).ResolveAllAsync(Source, CancellationToken.None);

        var verify = discoveries.Should().ContainSingle().Which.Verify;
        verify.Should().NotBeNull(
            "the read shape drops an undeclared key silently — the parser is the hop this "
            + "field dies in, so it is driven from real YAML");
        verify!.Select(stage => stage.Label).Should().Equal(["build", "bundle"],
            "the file states the order the run executes");
        verify[1].Command.Should().Be("databricks bundle validate");
        verify[1].WhenPresent.Should().Be("databricks.yml");
        verify[0].WhenPresent.Should().BeNull("an omitted condition is no condition");
    }

    /// <summary>
    /// p0261 `--context NAME` is the second production construction site of the discovery,
    /// and the one 2026-08-28-7b41 found dropping the field.
    /// </summary>
    [Fact]
    public async Task ContextYaml_AVerifyBlock_ReachesTheExplicitContextPathToo()
    {
        var discoveries = await Resolver(out _)
            .ResolveContextAsync(Source, "warehouse", CancellationToken.None);

        discoveries.Should().ContainSingle().Which.Verify.Should().NotBeNull()
            .And.HaveCount(2, "the pinned-context path builds the same record");
    }

    [Fact]
    public void ContextYaml_AStageMissingItsCommand_IsNotAStage()
    {
        var summary = new ContextYamlSerializer(new ContextYamlBuilders()).Parse("""
            meta:
              workdir: "."
            verify:
              - label: "build"
            """).Summary;

        summary!.Verify.Should().BeNull(
            "a half-record would travel to the resolver as something it has to re-check");
    }

    /// <summary>
    /// The tool description now asks a model for the block, so the write path has to emit
    /// one the reader gets back — the schema refuses what it does not declare, and the
    /// reader drops what its shape does not name.
    /// </summary>
    [Fact]
    public void ContextYaml_AVerifyBlockTheModelWrites_SurvivesTheWritePath()
    {
        var gate = TestHelpers.ContextGates.Build();
        gate.TryRead(System.Text.Json.JsonDocument.Parse("""
            {
              "meta": { "workdir": "." },
              "stack": { "lang": "python", "image": "python:3.12-bookworm" },
              "verify": [
                { "label": "lint", "command": "ruff check ." },
                { "label": "bundle", "command": "make bundle", "when_present": "bundle.yml" }
              ]
            }
            """).RootElement, out var typed, out var defect).Should().BeTrue("{0}", defect);
        gate.Defect(typed!).Should().BeNull("the schema declares the block the tool asks for");

        var yaml = TestHelpers.ContextGates.Serializer().Serialize(typed!);

        Architecture.ContextSchemaFile.Validate(yaml).Should().BeEmpty();
        new ContextYamlSerializer(new ContextYamlBuilders()).Parse(yaml).Summary!.Verify
            .Should().BeEquivalentTo(typed!.Verify,
                "what the writer emits is what the reader reads back — the one-builder rule");
    }

    /// <summary>
    /// The discoveries are checkpointed at <see cref="ContextKeys.RemoteContextInventory"/>
    /// and restored on resume, so a field that cannot round-trip is a field a resumed run
    /// silently verifies without.
    /// </summary>
    [Fact]
    public async Task ContextYaml_AVerifyBlock_SurvivesTheResumeRoundTrip()
    {
        var discoveries = await Resolver(out _).ResolveAllAsync(Source, CancellationToken.None);
        var before = new PipelineContext();
        before.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.RemoteContextInventory,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>> { ["repo"] = discoveries });
        var serializer = new PipelineContextSerializer(NullLogger<PipelineContextSerializer>.Instance);

        var after = new PipelineContext();
        serializer.Restore(serializer.Serialize(before), after);

        after.TryGet<Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.RemoteContextInventory, out var restored).Should().BeTrue();
        restored!["repo"].Single().Verify.Should().BeEquivalentTo(discoveries.Single().Verify,
            "a resumed run verifies by the same declaration the first attempt read");
    }
}
