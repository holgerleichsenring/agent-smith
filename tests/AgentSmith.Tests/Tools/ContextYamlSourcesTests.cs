using System.Text.Json;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// 2026-10-13-50c7: a context names the directories its component is built and tested from; the
/// field survives read, write and discovery, and a directory that is the root, rooted or escaping is refused.
/// </summary>
public sealed class ContextYamlSourcesTests
{
    private readonly ContextYamlSerializer _serializer = new(new ContextYamlBuilders());
    private readonly Mock<ISandbox> _sandbox = new();
    private Step? _written;

    public ContextYamlSourcesTests()
    {
        _sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                if (step.Kind == StepKind.WriteFile) _written = step;
                return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null));
            });
    }

    [Fact]
    public void RemoteContextDiscovery_SourcesDeclared_CarriesThem()
    {
        var parsed = _serializer.Parse("""
            meta:
              workdir: src/Inventory.Api
            sources:
              - src/Inventory.Core
              - " tests/Inventory.Tests "
            """);

        var discovery = RemoteContextDiscovery.From("api", parsed.Summary!);

        discovery.Sources.Should().Equal("src/Inventory.Core", "tests/Inventory.Tests");
    }

    [Fact]
    public void ContextYamlSerializer_NoSourcesBlock_ReadsNull() =>
        _serializer.Parse("meta:\n  workdir: src/Api\nsources: []\n").Summary!.Sources.Should().BeNull();

    [Theory]
    [InlineData(".")]
    [InlineData("./")]
    [InlineData("")]
    [InlineData("../lib")]
    [InlineData("src/../../lib")]
    [InlineData("/work/src")]
    public async Task ContextSchema_SourceDot_IsRefused(string source)
    {
        var result = await Host().WriteContextYaml("client", "default", Document(source));

        result.Should().NotStartWith(SandboxContextYamlWriter.WrittenPrefix);
        result.Should().Contain("/sources/0");
        _written.Should().BeNull();
    }

    [Fact]
    public async Task WriteContextYaml_Sources_AreWrittenAndReadBack()
    {
        var result = await Host().WriteContextYaml("client", "default", Document("src/Inventory.Core"));

        result.Should().StartWith(SandboxContextYamlWriter.WrittenPrefix);
        _serializer.Parse(_written!.Content!).Summary!.Sources.Should().Equal("src/Inventory.Core");
    }

    [Fact]
    public void WriteContextYamlToolDescription_DocumentsSources() =>
        WriteContextYamlToolDescription.Document.Should()
            .Contain("sources?: [dir,…]").And.Contain("READ them from the manifests");

    private static JsonElement Document(string source) => JsonDocument.Parse($$"""
        {
          "meta": { "workdir": "src/Inventory.Api" },
          "stack": { "lang": "csharp", "image": "mcr.microsoft.com/dotnet/sdk:10.0" },
          "sources": [ {{JsonSerializer.Serialize(source)}} ]
        }
        """).RootElement;

    private WriteContextYamlToolHost Host() => new(
        new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["client"] = _sandbox.Object },
        defaultRepo: "client", _serializer, ContextGates.Build(), ContextGates.Writer(),
        ContextGates.DerivationStamp(), ContextGates.ImageReview());
}
