using System.Text.Json;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// p0193: write_context_yaml takes JSON, emits YAML the parser reads back.
/// Round-trip is the central pin.
/// </summary>
public sealed class WriteContextYamlToolHostTests
{
    private readonly Mock<ISandbox> _sandboxMock = new();
    private readonly ContextYamlSerializer _serializer = new();

    public WriteContextYamlToolHostTests()
    {
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null)));
    }

    [Fact]
    public async Task WriteContextYaml_HappyPath_WritesCanonicalPath_AndYamlParsesBack()
    {
        var sut = BuildHost();
        var document = JsonDocument.Parse("""
            {
              "meta": { "workdir": "src/Client", "project": "AuthClient", "type": "Angular SPA" },
              "stack": { "lang": "TypeScript", "sdks": ["@azure/msal-angular", "rxjs"] }
            }
            """).RootElement;

        var result = await sut.WriteContextYaml(repo: "client", context_name: "default", document);

        result.Should().StartWith("context.yaml written:");
        result.Should().Contain(".agentsmith/contexts/default/context.yaml");

        Step? captured = null;
        _sandboxMock.Verify(s => s.RunStepAsync(
            It.Is<Step>(st => Capture(st, out captured) && st.Kind == StepKind.WriteFile),
            It.IsAny<IProgress<StepEvent>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        captured!.Path.Should().Be(".agentsmith/contexts/default/context.yaml");
        var parsed = _serializer.Parse(captured.Content!);
        parsed.ErrorReason.Should().BeNull();
        parsed.Summary!.Workdir.Should().Be("src/Client");
        parsed.Summary.Language.Should().Be("TypeScript");
    }

    [Fact]
    public async Task WriteContextYaml_MissingWorkdir_ReturnsError()
    {
        var sut = BuildHost();
        var document = JsonDocument.Parse("""
            { "meta": { "project": "x" }, "stack": { "lang": "C#" } }
            """).RootElement;

        var result = await sut.WriteContextYaml(repo: "client", context_name: "default", document);

        result.Should().StartWith("Error:");
        result.Should().Contain("Workdir is required");
    }

    [Fact]
    public async Task WriteContextYaml_ContextNameWithSlash_ReturnsError()
    {
        var sut = BuildHost();
        var document = JsonDocument.Parse("""{ "meta": { "workdir": "." } }""").RootElement;

        var result = await sut.WriteContextYaml(repo: "client", context_name: "evil/path", document);

        result.Should().Contain("must be a single path segment");
    }

    [Fact]
    public async Task WriteContextYaml_UnknownRepo_ReturnsError()
    {
        var sut = BuildHost();
        var document = JsonDocument.Parse("""{ "meta": { "workdir": "." } }""").RootElement;

        var result = await sut.WriteContextYaml(repo: "missing", context_name: "default", document);

        result.Should().Contain("unknown repo 'missing'");
    }

    [Fact]
    public async Task WriteContextYaml_InvalidJsonShape_ReturnsError()
    {
        var sut = BuildHost();
        var document = JsonDocument.Parse("""{ "wrong": "shape" }""").RootElement;

        var result = await sut.WriteContextYaml(repo: "client", context_name: "default", document);

        result.Should().StartWith("Error:");
    }

    private WriteContextYamlToolHost BuildHost()
    {
        var sandboxes = new Dictionary<string, ISandbox>(StringComparer.Ordinal)
        {
            ["client"] = _sandboxMock.Object,
        };
        return new WriteContextYamlToolHost(sandboxes, defaultRepo: "client", _serializer);
    }

    private static bool Capture(Step step, out Step? captured)
    {
        captured = step;
        return true;
    }
}
